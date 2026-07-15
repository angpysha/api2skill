using System.Net;
using System.Net.Sockets;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

[Collection("LoopbackHttp")]
public class LoopbackHttpCaptureTests
{
    [Fact]
    public async Task CapturesAuthorizationCode_FromLoopbackRedirect()
    {
        var port = GetFreePort();
        var callback = new Uri($"http://127.0.0.1:{port}/callback");
        var capture = new LoopbackHttpCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10), State: "expected-state");

        var captureTask = capture.CaptureAsync(options);

        // Give the listener a moment to bind
        await Task.Delay(50);
        using var client = new HttpClient();
        var response = await client.GetAsync(
            $"http://127.0.0.1:{port}/callback?code=THE_CODE&state=expected-state");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await captureTask;
        Assert.True(result.Ok);
        Assert.Equal(nameof(CaptureMode.HttpLoopback), result.Mode);
        Assert.Equal("THE_CODE", result.Code);
        Assert.Equal("expected-state", result.State);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task IgnoresFavicon_ThenCapturesCode()
    {
        var port = GetFreePort();
        var callback = new Uri($"http://127.0.0.1:{port}/callback");
        var capture = new LoopbackHttpCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10));

        var captureTask = capture.CaptureAsync(options);
        await Task.Delay(50);

        using var client = new HttpClient();
        var favicon = await client.GetAsync($"http://127.0.0.1:{port}/favicon.ico");
        Assert.Equal(HttpStatusCode.NotFound, favicon.StatusCode);

        _ = await client.GetAsync($"http://127.0.0.1:{port}/callback?code=AFTER_FAVICON&state=s");

        var result = await captureTask;
        Assert.True(result.Ok);
        Assert.Equal("AFTER_FAVICON", result.Code);
    }

    [Fact]
    public async Task IdpError_YieldsOkFalse()
    {
        var port = GetFreePort();
        var callback = new Uri($"http://127.0.0.1:{port}/callback");
        var capture = new LoopbackHttpCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10));

        var captureTask = capture.CaptureAsync(options);
        await Task.Delay(50);

        using var client = new HttpClient();
        _ = await client.GetAsync(
            $"http://127.0.0.1:{port}/callback?error=access_denied&error_description=cancelled");

        var result = await captureTask;
        Assert.False(result.Ok);
        Assert.Equal("access_denied", result.Error);
        Assert.Equal("cancelled", result.ErrorDescription);
        Assert.Null(result.Code);
    }

    [Fact]
    public async Task Timeout_YieldsTimeoutError()
    {
        var port = GetFreePort();
        var callback = new Uri($"http://127.0.0.1:{port}/callback");
        var capture = new LoopbackHttpCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromMilliseconds(200));

        var result = await capture.CaptureAsync(options);
        Assert.False(result.Ok);
        Assert.Equal("timeout", result.Error);
        Assert.Contains("seconds", result.ErrorDescription ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalhostPrefix_AlsoAccepts127()
    {
        var port = GetFreePort();
        var callback = new Uri($"http://localhost:{port}/callback");
        var capture = new LoopbackHttpCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10));

        var captureTask = capture.CaptureAsync(options);
        await Task.Delay(50);

        using var client = new HttpClient();
        // Hit 127.0.0.1 while registered as localhost — dual prefixes
        _ = await client.GetAsync($"http://127.0.0.1:{port}/callback?code=DUAL&state=s");

        var result = await captureTask;
        Assert.True(result.Ok);
        Assert.Equal("DUAL", result.Code);
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
