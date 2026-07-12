using System.Diagnostics;
using System.Net;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T067-T069: script auth profiles run a configured command on each call; trimmed stdout becomes
/// the header value (default <c>Authorization</c>), with optional <c>bearerPrefix</c>, and a
/// non-zero exit fails the call surfacing stderr.
/// </summary>
[Collection("LoopbackHttp")]
public class DispatcherScriptAuthTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-script-auth-" + Guid.NewGuid().ToString("N"));
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

    private static AuthProfile ScriptProfile(string command, bool bearerPrefix = false, string header = "Authorization") =>
        new("scriptAuth", AuthType.Script, Attachment.Global, null, null, null,
            new ScriptSettings(command, header, bearerPrefix), null);

    private static string PrintTokenCommand(string token) =>
        OperatingSystem.IsWindows()
            ? $"echo {token}"
            : $"printf '{token}'";

    private static string FailingCommandWithStderr(string message) =>
        OperatingSystem.IsWindows()
            ? $"cmd /c \"echo {message} 1>&2 & exit /b 1\""
            : $"sh -c 'echo {message} >&2; exit 1'";

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

    [Fact]
    public async Task ScriptProfile_TrimmedStdout_BecomesDefaultAuthorizationHeader()
    {
        var token = "script-tok-abc";
        var authConfig = new AuthConfig([ScriptProfile(PrintTokenCommand(token))]);
        var skillDir = await GenerateSkillAsync(authConfig);

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal(token, request.Headers["Authorization"]);
    }

    [Fact]
    public async Task ScriptProfile_BearerPrefix_AddsBearerOnceWhenAbsent()
    {
        var token = "tok-456";
        var authConfig = new AuthConfig([ScriptProfile(PrintTokenCommand(token), bearerPrefix: true)]);
        var skillDir = await GenerateSkillAsync(authConfig);

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal($"Bearer {token}", request.Headers["Authorization"]);
    }

    [Fact]
    public async Task ScriptProfile_BearerPrefix_DoesNotDoublePrefixWhenAlreadyPresent()
    {
        var prefixed = "Bearer already-there";
        var authConfig = new AuthConfig([ScriptProfile(PrintTokenCommand(prefixed), bearerPrefix: true)]);
        var skillDir = await GenerateSkillAsync(authConfig);

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync(skillDir, "apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal(prefixed, request.Headers["Authorization"]);
    }

    [Fact]
    public async Task ScriptProfile_NonZeroExit_FailsCallAndSurfacesStderr()
    {
        const string errorMessage = "script-auth-failure-sentinel";
        var authConfig = new AuthConfig([ScriptProfile(FailingCommandWithStderr(errorMessage))]);
        var skillDir = await GenerateSkillAsync(authConfig);

        var (exitCode, _, stderr) = await RunDispatcherAsync(skillDir, "apiKeyOp");

        Assert.Equal(2, exitCode);
        Assert.Contains(errorMessage, stderr, StringComparison.Ordinal);
    }
}
