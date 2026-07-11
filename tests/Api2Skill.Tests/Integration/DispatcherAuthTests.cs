using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T020: runs the *actual* generated call.cs as a subprocess (dotnet run) against a real
/// loopback HTTP listener and asserts on the headers the server received — the same check
/// performed manually during development, now automated. Complements AuthCodegenTests
/// (source-text assertions) with real execution coverage.
/// </summary>
public class DispatcherAuthTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-dispatcher-" + Guid.NewGuid().ToString("N"));
    private readonly HttpListener _listener = new();
    private int _port;
    private string _skillDir = "";

    public async Task InitializeAsync()
    {
        _port = GetFreeLoopbackPort();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();

        _skillDir = await GenerateMultiAuthSkillAsync();
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), $$"""
            {
              "apiKeyAuth": { "apiKey": "APIKEY_SENTINEL" },
              "bearerAuth": { "bearerToken": "BEARER_SENTINEL" },
              "basicAuth": { "username": "alice", "password": "s3cret" },
              "oauth2Auth": { "clientId": "client123", "clientSecret": "clientsecret456", "tokenUrl": "http://127.0.0.1:{{_port}}/oauth/token" }
            }
            """);
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

    private static int GetFreeLoopbackPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private async Task<string> GenerateMultiAuthSkillAsync()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "multi-auth.yaml");
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(fixturePath));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));

        var outDir = Path.Combine(_workDir, "skill");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());
        return outDir;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunDispatcherAsync(string operationId)
    {
        var psi = new ProcessStartInfo("dotnet", $"run scripts/call.cs -- {operationId}")
        {
            WorkingDirectory = _skillDir,
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

    [Fact]
    public async Task ApiKeyOp_SendsApiKeyHeader()
    {
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, stderr) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("APIKEY_SENTINEL", request.Headers["X-Api-Key"]);
        Assert.Null(request.Headers["Authorization"]);
        Assert.DoesNotContain("warning:", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BearerOp_SendsBearerAuthorizationHeader()
    {
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("bearerOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer BEARER_SENTINEL", request.Headers["Authorization"]);
    }

    [Fact]
    public async Task BasicOp_SendsBase64EncodedBasicAuthorizationHeader()
    {
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("basicOp");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Basic YWxpY2U6czNjcmV0", request.Headers["Authorization"]); // base64("alice:s3cret")
    }

    [Fact]
    public async Task Oauth2Op_FetchesTokenThenSendsItAsBearer()
    {
        var tokenTask = RespondToTokenRequestAsync();
        var apiRequestTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("oauth2Op");

        await tokenTask;
        var apiRequest = await apiRequestTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("Bearer TESTTOKEN123", apiRequest.Headers["Authorization"]);
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

    private async Task RespondToTokenRequestAsync()
    {
        var context = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"access_token\":\"TESTTOKEN123\",\"token_type\":\"bearer\"}");
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
    }
}
