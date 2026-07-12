using System.Diagnostics;
using System.Net;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T031-T034: runs the actual generated <c>call.cs</c> as a subprocess against a real loopback
/// listener with an explicit <c>auth.json</c> (bearer/basic/custom + override of spec-derived
/// auth). Complements <see cref="DispatcherAuthTests"/> (spec-derived-only auth).
/// </summary>
[Collection("LoopbackHttp")]
public class DispatcherExplicitAuthTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-explicit-auth-" + Guid.NewGuid().ToString("N"));
    private HttpListener _listener = null!;
    private int _port;

    public Task InitializeAsync()
    {
        (_listener, _port) = LoopbackHttpListenerFactory.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private async Task<string> GenerateSkillAsync(AuthConfig authConfig, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "multi-auth.yaml");
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(fixturePath));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth", AuthConfig: authConfig));

        var outDir = Path.Combine(_workDir, caller);
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(authConfig));
        return outDir;
    }

    private static async Task WriteSecretsAsync(string skillDir, string json) =>
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"), json);

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunDispatcherAsync(string skillDir, string operationId)
    {
        var psi = new ProcessStartInfo("dotnet", $"run scripts/call.cs -- {operationId}")
        {
            WorkingDirectory = skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["API2SKILL_BASE_URL"] = $"http://127.0.0.1:{_port}";

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private async Task<HttpListenerRequest> CaptureRequestAsync()
    {
        var context = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var request = context.Request;
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
        return request;
    }

    private static AuthProfile Bearer(string name, string secretRef = "{secret:MY_TOKEN}") =>
        new(name, AuthType.Bearer, Attachment.Global, new BearerSettings(secretRef), null, null, null, null);

    [Fact]
    public async Task BearerProfile_TokenLacksPrefix_SendsBearerPrefixedAuthorizationHeader()
    {
        var authConfig = new AuthConfig([Bearer("default")]);
        var skillDir = await GenerateSkillAsync(authConfig);
        await WriteSecretsAsync(skillDir, """{"MY_TOKEN":"abc123"}""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp"); // spec declares apiKey; explicit bearer overrides it

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer abc123", request.Headers["Authorization"]);
        Assert.Null(request.Headers["X-Api-Key"]); // spec-derived apiKey scheme was overridden, not additive
    }

    [Fact]
    public async Task BearerProfile_TokenAlreadyHasPrefix_DoesNotDoublePrefix()
    {
        var authConfig = new AuthConfig([Bearer("default")]);
        var skillDir = await GenerateSkillAsync(authConfig);
        await WriteSecretsAsync(skillDir, """{"MY_TOKEN":"Bearer already-prefixed"}""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer already-prefixed", request.Headers["Authorization"]);
    }

    [Fact]
    public async Task BasicProfile_SendsBase64EncodedAuthorizationHeader()
    {
        var profile = new AuthProfile("default", AuthType.Basic, Attachment.Global, null,
            new BasicSettings("{secret:USER}", "{secret:PASS}"), null, null, null);
        var skillDir = await GenerateSkillAsync(new AuthConfig([profile]));
        await WriteSecretsAsync(skillDir, """{"USER":"alice","PASS":"s3cret"}""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "basicOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Basic YWxpY2U6czNjcmV0", request.Headers["Authorization"]);
    }

    [Fact]
    public async Task CustomProfile_SendsMultipleDistinctHeaders()
    {
        var profile = new AuthProfile("gw", AuthType.Custom, Attachment.Global, null, null,
            new CustomSettings([
                new HeaderEntry("Authorization", "{secret:A}"),
                new HeaderEntry("ApiKey", "{secret:K}"),
            ]), null, null);
        var skillDir = await GenerateSkillAsync(new AuthConfig([profile]));
        await WriteSecretsAsync(skillDir, """{"A":"tok-a","K":"key-k"}""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "headerEchoOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("tok-a", request.Headers["Authorization"]);
        Assert.Equal("key-k", request.Headers["ApiKey"]);
    }

    [Fact]
    public async Task BearerAndCustomStacked_BothHeadersPresentOnSameRequest()
    {
        var authConfig = new AuthConfig([
            Bearer("user", "{secret:USER_TOKEN}"),
            new AuthProfile("gw", AuthType.Custom, Attachment.Global, null, null,
                new CustomSettings([new HeaderEntry("ApiKey", "{secret:GW_KEY}")]), null, null),
        ]);
        var skillDir = await GenerateSkillAsync(authConfig);
        await WriteSecretsAsync(skillDir, """{"USER_TOKEN":"utok","GW_KEY":"gwkey"}""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer utok", request.Headers["Authorization"]);
        Assert.Equal("gwkey", request.Headers["ApiKey"]);
    }

    [Fact]
    public async Task UncoveredOperation_KeepsSpecDerivedAuth()
    {
        // Only "apiKeyOp" is covered by the explicit profile (attached globally would cover all,
        // so use a tag attachment that matches nothing to leave every operation uncovered,
        // proving the spec-derived fallback still applies).
        var profile = new AuthProfile("unused", AuthType.Bearer,
            new Attachment(AttachScope.Tags, ["NoSuchTag"]), new BearerSettings("{secret:T}"), null, null, null, null);
        var skillDir = await GenerateSkillAsync(new AuthConfig([profile]));
        await WriteSecretsAsync(skillDir, """
            { "T": "unused-token", "apiKeyAuth": { "apiKey": "APIKEY_SENTINEL" } }
            """);

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("APIKEY_SENTINEL", request.Headers["X-Api-Key"]); // spec-derived, unaffected
        Assert.Null(request.Headers["Authorization"]);
    }

    [Fact]
    public async Task MissingReferencedSecret_FailsTheCall_WithExitCodeTwo_NamingTheKey()
    {
        var authConfig = new AuthConfig([Bearer("default", "{secret:NOT_PRESENT}")]);
        var skillDir = await GenerateSkillAsync(authConfig);
        await WriteSecretsAsync(skillDir, "{}");

        var (exitCode, _, stderr) = await RunDispatcherAsync(skillDir, "apiKeyOp");

        Assert.Equal(2, exitCode);
        Assert.Contains("NOT_PRESENT", stderr, StringComparison.Ordinal);
    }
}
