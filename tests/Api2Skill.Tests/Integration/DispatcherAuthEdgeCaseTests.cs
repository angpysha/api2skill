using System.Diagnostics;
using System.Net;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// Adversarial auth-path coverage beyond the happy path already exercised in
/// Integration/DispatcherAuthTests: a malformed <c>secrets.json</c> and an OAuth2 token
/// endpoint that returns something other than a clean 2xx JSON body with an
/// <c>access_token</c> property. Runs the *actual* generated <c>call.cs</c> as a subprocess
/// (dotnet run) against a real loopback listener, the same way DispatcherAuthTests does, so
/// these assert on real process exit codes/stderr rather than on generated source text.
/// </summary>
[Collection("LoopbackHttp")]
public class DispatcherAuthEdgeCaseTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-dispatcher-edge-" + Guid.NewGuid().ToString("N"));
    private HttpListener _listener = null!;
    private int _port;
    private string _skillDir = "";

    public async Task InitializeAsync()
    {
        (_listener, _port) = LoopbackHttpListenerFactory.Start();
        _skillDir = await GenerateMultiAuthSkillAsync();
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

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunDispatcherAsync(string operationId, params string[] extraArgs)
    {
        // Uses ArgumentList (not the single-string Arguments form) so values containing raw
        // CR/LF are delivered to the child process as a single literal argument, unmolested by
        // any shell-style quoting/splitting — the only reliable way to prove the dispatcher's
        // own header-value guard (not this test's argument passing) is what rejects the value.
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("run");
        psi.ArgumentList.Add("scripts/call.cs");
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(operationId);
        foreach (var arg in extraArgs)
        {
            psi.ArgumentList.Add(arg);
        }
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
    public async Task MalformedSecretsJson_DoesNotCrashWithAnUnhandledException()
    {
        // secrets.json exists but is not valid JSON at all (e.g. a user typo'd it by hand).
        // Before the fix, LoadSecrets() called JsonDocument.Parse with no try/catch, entirely
        // outside the dispatcher's own try/catch(HttpRequestException) block, so this crashed
        // with a raw unhandled-exception stack trace instead of a clean, documented failure.
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), "{ this is not json");

        // Once the crash is fixed, secrets are treated as absent and the dispatcher proceeds
        // to make the (now-unauthenticated) call, so a listener accept is still needed.
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("apiKeyOp");

        var request = await serverTask;
        var (exitCode, _, stderr) = await runTask;

        Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Null(request.Headers["X-Api-Key"]);
        Assert.Contains("warning:", stderr, StringComparison.Ordinal);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task OAuth2TokenEndpoint_ReturningMalformedJson_DoesNotCrash_AndWarnsInstead()
    {
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), $$"""
            { "oauth2Auth": { "clientId": "c", "clientSecret": "s", "tokenUrl": "http://127.0.0.1:{{_port}}/oauth/token" } }
            """);

        // Sequential accepts (see the non-success-status test for why): first accept is
        // guaranteed to be the token request, second the real (now-unauthenticated) op call —
        // once the fix stops the crash, the dispatcher proceeds to make that second call.
        var runTask = RunDispatcherAsync("oauth2Op");

        var tokenContext = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
        tokenContext.Response.StatusCode = 200;
        tokenContext.Response.ContentType = "text/html";
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes("<html>not json at all</html>");
        await tokenContext.Response.OutputStream.WriteAsync(tokenBytes);
        tokenContext.Response.OutputStream.Close();

        var apiRequest = await CaptureRequestAsync();
        var (exitCode, _, stderr) = await runTask;

        Assert.DoesNotContain("Unhandled exception", stderr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("failed to obtain an OAuth2 token", stderr, StringComparison.Ordinal);
        Assert.Null(apiRequest.Headers["Authorization"]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task OAuth2TokenEndpoint_ReturningNonSuccessStatus_SendsUnauthenticatedRatherThanCrashing()
    {
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), $$"""
            { "oauth2Auth": { "clientId": "c", "clientSecret": "s", "tokenUrl": "http://127.0.0.1:{{_port}}/oauth/token" } }
            """);

        // Accept connections strictly sequentially rather than racing two concurrent
        // GetContextAsync() calls: the dispatcher itself is single-threaded and always issues
        // the token request before the real operation request, so the first accept here is
        // guaranteed to be the token call and the second the real call — racing two pending
        // accepts (as DispatcherAuthTests does for the symmetric-response happy path) would be
        // unsafe here since the two responses are deliberately different.
        var runTask = RunDispatcherAsync("oauth2Op");

        var tokenContext = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
        tokenContext.Response.StatusCode = 401;
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes("{\"error\":\"invalid_client\"}");
        await tokenContext.Response.OutputStream.WriteAsync(tokenBytes);
        tokenContext.Response.OutputStream.Close();

        var apiRequest = await CaptureRequestAsync();
        var (exitCode, _, stderr) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Null(apiRequest.Headers["Authorization"]);
        Assert.Contains("failed to obtain an OAuth2 token", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeaderParameter_WithCrLf_IsRejected_RatherThanSentToTheServer()
    {
        // SEC-002 (Phase 8.5 security review): header-parameter values come straight from
        // untrusted runtime CLI args (an LLM agent, in the dispatcher's real usage), and were
        // added via Headers.TryAddWithoutValidation with no CRLF check — the one HttpHeaders API
        // that explicitly skips .NET's own header-value validation. This proves the dispatcher's
        // own guard now rejects such a value before ever touching the network, rather than
        // relying solely on the transport layer as a backstop.
        var runTask = RunDispatcherAsync("headerEchoOp", "--X-Custom", "evil\r\nX-Injected: true");

        var (exitCode, _, stderr) = await runTask;

        Assert.Equal(2, exitCode);
        Assert.Contains("control characters are not allowed", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeaderParameter_WithoutControlCharacters_IsSentNormally()
    {
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("headerEchoOp", "--X-Custom", "plain-value");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("plain-value", request.Headers["X-Custom"]);
    }

    [Fact]
    public async Task MissingRequiredSecretKey_ForSchemeInUse_SendsUnauthenticatedWithAWarning_NotAnException()
    {
        // basicAuth requires BOTH username and password; supply only one.
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"),
            """{ "basicAuth": { "username": "alice" } }""");

        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("basicOp");

        var request = await serverTask;
        var (exitCode, _, stderr) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Null(request.Headers["Authorization"]);
        Assert.Contains("warning: no username/password configured for scheme 'basicAuth'", stderr, StringComparison.Ordinal);
    }
}
