using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Api2Skill.Cli;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.Cli;

public class OAuthCaptureHttpCliTests
{
    [Fact]
    public async Task MissingCallbackUrl_ExitsUsageError()
    {
        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: null,
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task UnknownMode_ExitsUsageError()
    {
        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: "http://127.0.0.1:9/callback",
            mode: "nope",
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task HttpsWithoutCert_NonInteractive_ExitsUsageError()
    {
        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: "https://127.0.0.1:8443/callback",
            isInteractive: false,
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task HttpCapture_WritesJson_AndExitsZero()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var runTask = OAuthCaptureCommand.RunAsync(
            callbackUrl: callback,
            timeoutSeconds: 10,
            stdout: stdout,
            stderr: stderr,
            isInteractive: false);

        await Task.Delay(80);
        using var client = new HttpClient();
        _ = await client.GetAsync($"{callback}?code=CLI_CODE&state=st");

        var exit = await runTask;
        Assert.Equal(ExitCodes.Success, exit);

        var result = JsonSerializer.Deserialize(stdout.ToString().Trim(), CaptureResultJsonContext.Default.CaptureResult);
        Assert.NotNull(result);
        Assert.True(result.Ok);
        Assert.Equal("CLI_CODE", result.Code);
        Assert.Equal(nameof(CaptureMode.HttpLoopback), result.Mode);
    }

    [Fact]
    public async Task HttpCapture_IdpError_ExitsSeven()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var stdout = new StringWriter();

        var runTask = OAuthCaptureCommand.RunAsync(
            callbackUrl: callback,
            timeoutSeconds: 10,
            stdout: stdout,
            stderr: TextWriter.Null,
            isInteractive: false);

        await Task.Delay(80);
        using var client = new HttpClient();
        _ = await client.GetAsync($"{callback}?error=access_denied&error_description=nope");

        var exit = await runTask;
        Assert.Equal(ExitCodes.OAuthRedirectError, exit);

        var result = JsonSerializer.Deserialize(stdout.ToString().Trim(), CaptureResultJsonContext.Default.CaptureResult);
        Assert.NotNull(result);
        Assert.False(result.Ok);
        Assert.Equal("access_denied", result.Error);
    }

    [Fact]
    public async Task HttpCapture_Timeout_WithInjectedCapture_ExitsSix()
    {
        var stdout = new StringWriter();
        var fake = new FakeTimeoutCapture();

        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: "http://127.0.0.1:9/callback",
            timeoutSeconds: 1,
            stdout: stdout,
            stderr: TextWriter.Null,
            httpCapture: fake,
            isInteractive: false);

        Assert.Equal(ExitCodes.CaptureTimeout, exit);
        Assert.Contains("\"error\":\"timeout\"", stdout.ToString(), StringComparison.Ordinal);
    }

    private sealed class FakeTimeoutCapture : IRedirectCapture
    {
        public Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CaptureResult(
                Ok: false,
                Mode: nameof(CaptureMode.HttpLoopback),
                Code: null,
                State: null,
                Error: "timeout",
                ErrorDescription: "No redirect received within 1 seconds",
                CallbackUrl: options.CallbackUrl.ToString()));
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
