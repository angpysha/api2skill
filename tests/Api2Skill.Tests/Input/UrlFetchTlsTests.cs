using System.Net;
using System.Net.Sockets;
using System.Text;
using Api2Skill.Input;

namespace Api2Skill.Tests.Input;

/// <summary>
/// T027: URL spec acquisition + the untrusted-HTTPS opt-in (EC-8/FR-007). The plain-HTTP fetch
/// path is covered here with a real in-process <see cref="HttpListener"/>. The self-signed-TLS
/// scenario (fails without --insecure, succeeds with it) was verified manually against a real
/// openssl-generated certificate — HttpListener's HTTPS binding is OS-specific enough
/// (particularly on macOS/Linux) that reproducing it reliably in-process here would trade a
/// real regression guard for a flaky one; see specs/001-openapi-to-skill/quickstart.md
/// Scenario 5 for the documented manual check.
/// </summary>
public class UrlFetchTlsTests : IAsyncLifetime
{
    private readonly HttpListener _listener = new();
    private int _port;

    public Task InitializeAsync()
    {
        _port = GetFreeLoopbackPort();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        return Task.CompletedTask;
    }

    private static int GetFreeLoopbackPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private async Task ServeOnceAsync(string body, string contentType = "application/json")
    {
        var context = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
        context.Response.StatusCode = 200;
        context.Response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(body);
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
    }

    [Fact]
    public async Task AcquireUrlAsync_FetchesAndBuffersTheResponseBody()
    {
        const string specJson = """{"openapi":"3.0.3","info":{"title":"t","version":"1"},"paths":{}}""";
        var serveTask = ServeOnceAsync(specJson);

        var (stream, format) = await SpecSource.AcquireUrlAsync($"http://127.0.0.1:{_port}/spec.json", insecure: false, formatOverride: null);
        await serveTask;

        Assert.Equal("json", format);
        using var reader = new StreamReader(stream);
        Assert.Equal(specJson, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task AcquireUrlAsync_SniffsYamlFromContent_WhenNoExtensionHint()
    {
        const string specYaml = "openapi: 3.0.3\ninfo:\n  title: t\n  version: \"1\"\npaths: {}\n";
        var serveTask = ServeOnceAsync(specYaml, "text/yaml");

        var (_, format) = await SpecSource.AcquireUrlAsync($"http://127.0.0.1:{_port}/spec", insecure: false, formatOverride: null);
        await serveTask;

        Assert.Equal("yaml", format);
    }

    [Fact]
    public async Task AcquireUrlAsync_ThrowsHttpRequestException_OnNonSuccessStatus()
    {
        var serveTask = Task.Run(async () =>
        {
            var context = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
            context.Response.StatusCode = 404;
            context.Response.OutputStream.Close();
        });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            SpecSource.AcquireUrlAsync($"http://127.0.0.1:{_port}/missing", insecure: false, formatOverride: null));
        await serveTask;
    }
}
