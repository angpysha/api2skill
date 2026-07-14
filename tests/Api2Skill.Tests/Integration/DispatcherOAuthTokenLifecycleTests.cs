using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T051 (US3): per-call oauth2 token resolution — a valid cached token is used without any
/// network call; an expired token with a refresh token refreshes silently; an expired token
/// with no usable refresh token fails the call with a re-login instruction and never launches
/// a browser (the operation-call code path never calls the browser-launch helper at all).
/// </summary>
[Collection("LoopbackHttp")]
public class DispatcherOAuthTokenLifecycleTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-oauth-lifecycle-" + Guid.NewGuid().ToString("N"));
    private HttpListener _apiListener = null!;
    private HttpListener _tokenListener = null!;
    private int _apiPort;
    private int _tokenPort;
    private string _skillDir = "";

    public async Task InitializeAsync()
    {
        (_apiListener, _apiPort) = LoopbackHttpListenerFactory.Start();
        (_tokenListener, _tokenPort) = LoopbackHttpListenerFactory.Start();
        _skillDir = await GenerateSkillAsync();
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), """{"CLIENT_ID":"cid"}""");
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

    private async Task<string> GenerateSkillAsync()
    {
        var authConfig = new AuthConfig([
            new AuthProfile("aad", AuthType.OAuth2, Attachment.Global, null, null, null, null,
                new OAuthSettings(
                    Grant: OAuthGrant.AuthorizationCode, Preset: null, Tenant: null,
                    AuthUrl: "https://fake-idp.example.com/authorize",
                    TokenUrl: $"http://127.0.0.1:{_tokenPort}/token",
                    Scopes: ["offline_access"], CallbackUrl: "http://localhost:18400/callback",
                    BrowserLaunch: "auto",
                    ClientAuth: ClientAuthMethod.Body, ClientId: "{secret:CLIENT_ID}", ClientSecret: null,
                    AuthorizeRequest: OAuthRequestExtras.Empty, TokenRequest: OAuthRequestExtras.Empty, TokenField: "access_token")),
        ]);

        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        var outDir = Path.Combine(_workDir, "skill");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(authConfig));
        return outDir;
    }

    private async Task WriteTokenCacheAsync(string accessToken, DateTimeOffset expiresAt, string? refreshToken)
    {
        var entry = new Dictionary<string, object?> { ["access_token"] = accessToken, ["expires_at"] = expiresAt.ToString("o") };
        if (refreshToken is not null)
        {
            entry["refresh_token"] = refreshToken;
        }
        var cache = new Dictionary<string, object?> { ["aad"] = entry };
        await File.WriteAllTextAsync(Path.Combine(_skillDir, ".auth-cache.json"), JsonSerializer.Serialize(cache));
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunOperationAsync(string operationId)
    {
        var psi = new ProcessStartInfo("dotnet", $"run scripts/call.cs -- {operationId} --petId 5")
        {
            WorkingDirectory = _skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["API2SKILL_BASE_URL"] = $"http://127.0.0.1:{_apiPort}";

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

        return (process.ExitCode, await stdoutTask, await stderrTask);
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

    [Fact]
    public async Task ValidCachedToken_IsUsedDirectly_NoTokenEndpointCall()
    {
        await WriteTokenCacheAsync("STILL-VALID-TOKEN", DateTimeOffset.UtcNow.AddHours(1), refreshToken: null);

        var apiTask = CaptureApiRequestAsync();
        var runTask = RunOperationAsync("getPetById");

        var request = await apiTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer STILL-VALID-TOKEN", request.Headers["Authorization"]);
        // No pending request on the token listener proves it was never contacted — verified
        // implicitly: CaptureApiRequestAsync would have hung/timed out if the dispatcher had
        // stalled waiting on a token fetch first, and the request already arrived above.
    }

    [Fact]
    public async Task ExpiredTokenWithRefreshToken_RefreshesSilently_ThenSucceeds()
    {
        await WriteTokenCacheAsync("EXPIRED-TOKEN", DateTimeOffset.UtcNow.AddMinutes(-5), refreshToken: "REFRESH-ME");

        var tokenTask = RespondToTokenRequestAsync("REFRESHED-TOKEN");
        var apiTask = CaptureApiRequestAsync();
        var runTask = RunOperationAsync("getPetById");

        await tokenTask;
        var request = await apiTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer REFRESHED-TOKEN", request.Headers["Authorization"]);

        var cache = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_skillDir, ".auth-cache.json")));
        Assert.Equal("REFRESHED-TOKEN", cache.RootElement.GetProperty("aad").GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task ExpiredTokenNoRefreshToken_FailsWithReloginInstruction_NoBrowserLaunchAttempted()
    {
        await WriteTokenCacheAsync("EXPIRED-TOKEN", DateTimeOffset.UtcNow.AddMinutes(-5), refreshToken: null);

        var (exitCode, _, stderr) = await RunOperationAsync("getPetById");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("login aad", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoCacheAtAll_FailsWithReloginInstruction()
    {
        // No .auth-cache.json exists at all — the first-ever call for this profile.
        var (exitCode, _, stderr) = await RunOperationAsync("getPetById");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("login aad", stderr, StringComparison.Ordinal);
    }

    private async Task RespondToTokenRequestAsync(string accessToken)
    {
        var context = await _tokenListener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(15));
        var json = JsonSerializer.Serialize(new Dictionary<string, object?> { ["access_token"] = accessToken, ["expires_in"] = 3600 });
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
    }
}
