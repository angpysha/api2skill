using System.Diagnostics;
using System.Net;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T032: runs the *actual* generated .fsx (via <c>dotnet fsi</c>) and .csx (via
/// <c>dotnet script</c>) dispatchers as subprocesses against a real loopback listener —
/// the fsx/csx counterpart to Auth/DispatcherAuthTests, kept to one auth scheme each
/// (dotnet fsi / dotnet script startup is materially slower than `dotnet run` on a compiled
/// .cs project; the full four-scheme matrix is already proven once, for .cs — this is about
/// proving each *translation* is correct, not re-proving the shared model/codegen approach).
/// </summary>
[Collection("LoopbackHttp")]
public class EmitterRunnerTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-emitrun-" + Guid.NewGuid().ToString("N"));
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

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private async Task<string> GenerateSkillAsync(IScriptEmitter emitter)
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));

        var outDir = Path.Combine(_workDir, emitter.Key);
        SkillWriter.Write(model, outDir, force: false, emitter);
        return outDir;
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

    private static async Task<(int ExitCode, string Stderr)> RunAsync(string fileName, string arguments, string workingDirectory, string baseUrl)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["API2SKILL_BASE_URL"] = baseUrl;

        using var process = Process.Start(psi)!;
        var stderrTask = process.StandardError.ReadToEndAsync();
        _ = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(120));

        return (process.ExitCode, await stderrTask);
    }

    [Fact]
    public async Task FsxDispatcher_AppliesApiKeyAuth_WhenRunViaFsi()
    {
        var skillDir = await GenerateSkillAsync(new FsxEmitter());
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"),
            """{ "apiKeyAuth": { "apiKey": "APIKEY_SENTINEL" } }""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunAsync("dotnet", "fsi scripts/call.fsx -- apiKeyOp", skillDir, $"http://127.0.0.1:{_port}");

        var request = await serverTask;
        var (exitCode, stderr) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("APIKEY_SENTINEL", request.Headers["X-Api-Key"]);
        Assert.DoesNotContain("warning:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CsxDispatcher_AppliesBearerAuth_WhenRunViaDotnetScript()
    {
        var skillDir = await GenerateSkillAsync(new CsxEmitter());
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"),
            """{ "bearerAuth": { "bearerToken": "BEARER_SENTINEL" } }""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunAsync("dotnet", "script scripts/call.csx -- bearerOp", skillDir, $"http://127.0.0.1:{_port}");

        var request = await serverTask;
        var (exitCode, stderr) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer BEARER_SENTINEL", request.Headers["Authorization"]);
        Assert.DoesNotContain("warning:", stderr, StringComparison.Ordinal);
    }
}
