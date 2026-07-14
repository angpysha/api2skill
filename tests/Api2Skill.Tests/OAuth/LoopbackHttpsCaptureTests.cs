using System.Net;
using System.Net.Sockets;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

public class LoopbackHttpsCaptureTests
{
    [Fact]
    public async Task CapturesAuthorizationCode_OverHttps()
    {
        using var cert = CertMaterialTests.CreateSelfSigned("CN=localhost");
        var port = GetFreePort();
        var callback = new Uri($"https://127.0.0.1:{port}/callback");
        var capture = new LoopbackHttpsCapture(cert);
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(15), State: "st");
        var captureTask = capture.CaptureAsync(options);
        await WaitForListenAsync(port);
        using var client = CreateInsecureHttpsClient();
        var response = await client.GetAsync($"https://127.0.0.1:{port}/callback?code=HTTPS_CODE&state=st");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await captureTask;
        Assert.True(result.Ok);
        Assert.Equal(nameof(CaptureMode.HttpsLoopback), result.Mode);
        Assert.Equal("HTTPS_CODE", result.Code);
    }

    [Fact]
    public async Task IgnoresFavicon_ThenCapturesCode()
    {
        using var cert = CertMaterialTests.CreateSelfSigned("CN=localhost");
        var port = GetFreePort();
        var capture = new LoopbackHttpsCapture(cert);
        var options = new CaptureOptions(new Uri($"https://127.0.0.1:{port}/callback"), Timeout: TimeSpan.FromSeconds(15));
        var captureTask = capture.CaptureAsync(options);
        await WaitForListenAsync(port);
        using var client = CreateInsecureHttpsClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"https://127.0.0.1:{port}/favicon.ico")).StatusCode);
        _ = await client.GetAsync($"https://127.0.0.1:{port}/callback?code=AFTER_FAVICON&state=s");
        Assert.Equal("AFTER_FAVICON", (await captureTask).Code);
    }

    [Fact]
    public async Task IdpError_YieldsOkFalse()
    {
        using var cert = CertMaterialTests.CreateSelfSigned("CN=localhost");
        var port = GetFreePort();
        var capture = new LoopbackHttpsCapture(cert);
        var options = new CaptureOptions(new Uri($"https://127.0.0.1:{port}/callback"), Timeout: TimeSpan.FromSeconds(15));
        var captureTask = capture.CaptureAsync(options);
        await WaitForListenAsync(port);
        using var client = CreateInsecureHttpsClient();
        _ = await client.GetAsync($"https://127.0.0.1:{port}/callback?error=access_denied&error_description=cancelled");
        var result = await captureTask;
        Assert.False(result.Ok);
        Assert.Equal("access_denied", result.Error);
    }

    [Fact]
    public async Task Timeout_YieldsTimeoutError()
    {
        using var cert = CertMaterialTests.CreateSelfSigned("CN=localhost");
        var port = GetFreePort();
        var capture = new LoopbackHttpsCapture(cert);
        var result = await capture.CaptureAsync(new CaptureOptions(new Uri($"https://127.0.0.1:{port}/callback"), Timeout: TimeSpan.FromMilliseconds(300)));
        Assert.False(result.Ok);
        Assert.Equal("timeout", result.Error);
        Assert.Equal(nameof(CaptureMode.HttpsLoopback), result.Mode);
    }

    [Fact]
    public async Task LocalhostCallback_Accepts127Redirect()
    {
        using var cert = CertMaterialTests.CreateSelfSigned("CN=localhost");
        var port = GetFreePort();
        var capture = new LoopbackHttpsCapture(cert);
        var captureTask = capture.CaptureAsync(new CaptureOptions(new Uri($"https://localhost:{port}/callback"), Timeout: TimeSpan.FromSeconds(15)));
        await WaitForListenAsync(port);
        using var client = CreateInsecureHttpsClient();
        _ = await client.GetAsync($"https://127.0.0.1:{port}/callback?code=DUAL&state=s");
        Assert.Equal("DUAL", (await captureTask).Code);
    }

    [Fact]
    public async Task RejectsNonHttpsCallbackUrl()
    {
        using var cert = CertMaterialTests.CreateSelfSigned("CN=localhost");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new LoopbackHttpsCapture(cert).CaptureAsync(new CaptureOptions(new Uri("http://127.0.0.1:9/callback"))));
    }

    private static HttpClient CreateInsecureHttpsClient() =>
        new(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
        { Timeout = TimeSpan.FromSeconds(10) };

    private static async Task WaitForListenAsync(int port)
    {
        for (var i = 0; i < 50; i++)
        {
            try
            {
                using var probe = new TcpClient();
                await probe.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch { await Task.Delay(40); }
        }
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
