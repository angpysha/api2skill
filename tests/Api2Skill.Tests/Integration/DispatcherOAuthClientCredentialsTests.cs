using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>T052 (US3): <c>client_credentials</c> tokens are fetched on demand at call time (no
/// interactive login) and cached like any other oauth2 token.</summary>
[Collection("LoopbackHttp")]
public class DispatcherOAuthClientCredentialsTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-oauth-cc-" + Guid.NewGuid().ToString("N"));
    private HttpListener _apiListener = null!;
    private HttpListener _tokenListener = null!;
    private int _apiPort;
    private int _tokenPort;
    private string _skillDir = "";

    public async Task InitializeAsync()
    {
        (_apiListener, _apiPort) = LoopbackHttpListenerFactory.Start();
        (_tokenListener, _tokenPort) = LoopbackHttpListenerFactory.Start();

        var authConfig = new AuthConfig([
            new AuthProfile("svc", AuthType.OAuth2, Attachment.Global, null, null, null, null,
                new OAuthSettings(
                    Grant: OAuthGrant.ClientCredentials, Preset: null, Tenant: null, AuthUrl: null,
                    TokenUrl: $"http://127.0.0.1:{_tokenPort}/token", Scopes: [],
                    CallbackUrl: "http://localhost:18401/callback", BrowserLaunch: "auto", ClientAuth: ClientAuthMethod.Body,
                    ClientId: "{secret:CLIENT_ID}", ClientSecret: "{secret:CLIENT_SECRET}",
                    AuthorizeRequest: OAuthRequestExtras.Empty, TokenRequest: OAuthRequestExtras.Empty, TokenField: "access_token")),
        ]);

        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        _skillDir = Path.Combine(_workDir, "skill");
        SkillWriter.Write(model, _skillDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(authConfig));
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), """{"CLIENT_ID":"cid","CLIENT_SECRET":"csecret"}""");
    }

    public Task DisposeAsync()
    {
        _apiListener.Stop();
        _apiListener.Close();
        _tokenListener.Stop();
        _tokenListener.Close();
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ClientCredentialsProfile_FetchesTokenOnDemand_AndUsesIt()
    {
        var tokenTask = RespondToTokenRequestAsync();
        var apiTask = CaptureApiRequestAsync();

        var psi = new ProcessStartInfo("dotnet", "run scripts/call.cs -- getPetById --petId 5")
        {
            WorkingDirectory = _skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["API2SKILL_BASE_URL"] = $"http://127.0.0.1:{_apiPort}";
        using var process = Process.Start(psi)!;

        var (form, request) = (await tokenTask, await apiTask);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, process.ExitCode);
        Assert.Equal("Bearer SERVICE-TOKEN", request.Headers["Authorization"]);
        Assert.Contains("grant_type=client_credentials", form, StringComparison.Ordinal);
        Assert.Contains("client_id=cid", form, StringComparison.Ordinal);
        Assert.Contains("client_secret=csecret", form, StringComparison.Ordinal);

        var cache = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_skillDir, ".auth-cache.json")));
        Assert.Equal("SERVICE-TOKEN", cache.RootElement.GetProperty("svc").GetProperty("access_token").GetString());
    }

    private async Task<string> RespondToTokenRequestAsync()
    {
        var context = await _tokenListener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(15));
        var form = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
        var json = JsonSerializer.Serialize(new Dictionary<string, object?> { ["access_token"] = "SERVICE-TOKEN", ["expires_in"] = 3600 });
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
        return form;
    }

    [Fact]
    public async Task LoginOnClientCredentialsProfile_ReportsNotApplicable_ExitsZero()
    {
        var psi = new ProcessStartInfo("dotnet", "run scripts/call.cs -- login svc")
        {
            WorkingDirectory = _skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("not applicable", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("client_credentials", stdout, StringComparison.Ordinal);
    }

    private async Task<HttpListenerRequest> CaptureApiRequestAsync()
    {
        var context = await _apiListener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(15));
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
        return context.Request;
    }
}
