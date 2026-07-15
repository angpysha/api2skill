using System.Net.Http;
using Api2Skill.Cli;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

/// <summary>T030 — hosted session/poll client against in-process stub.</summary>
[Collection("LoopbackHttp")]
public class HostedRelayCaptureTests
{
    [Fact]
    public async Task CapturesAuthorizationCode_ViaSessionAndPoll()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var http = new HttpClient();
        var progress = new StringWriter();
        var capture = new HostedRelayCapture(http, progress, pollInterval: TimeSpan.FromMilliseconds(25));

        var callback = new Uri(server.BaseUri, "v1/callback");
        var options = new CaptureOptions(
            CallbackUrl: callback,
            Mode: CaptureMode.Hosted,
            Timeout: TimeSpan.FromSeconds(10),
            RelayBaseUrl: server.BaseUri.ToString().TrimEnd('/'),
            State: "expected-state");

        var captureTask = capture.CaptureAsync(options);

        // Wait until session callback URL appears on progress (sid ready)
        Uri? sessionCallback = null;
        for (var i = 0; i < 40 && sessionCallback is null; i++)
        {
            await Task.Delay(25);
            var text = progress.ToString();
            var marker = "Hosted callback URL: ";
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var line = text[(idx + marker.Length)..].Split('\n', '\r')[0].Trim();
                if (Uri.TryCreate(line, UriKind.Absolute, out var u))
                {
                    sessionCallback = u;
                }
            }
        }

        Assert.NotNull(sessionCallback);
        using var browser = new HttpClient();
        var hit = await browser.GetAsync($"{sessionCallback}&code=HOSTED_CODE&state=expected-state");
        hit.EnsureSuccessStatusCode();

        var result = await captureTask;
        Assert.True(result.Ok);
        Assert.Equal(nameof(CaptureMode.Hosted), result.Mode);
        Assert.Equal("HOSTED_CODE", result.Code);
        Assert.Equal("expected-state", result.State);
        Assert.Null(result.Error);
        Assert.Contains("sid=", result.CallbackUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdpError_YieldsOkFalse()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var http = new HttpClient();
        var progress = new StringWriter();
        var capture = new HostedRelayCapture(http, progress, pollInterval: TimeSpan.FromMilliseconds(25));

        var options = new CaptureOptions(
            CallbackUrl: new Uri(server.BaseUri, "v1/callback"),
            Mode: CaptureMode.Hosted,
            Timeout: TimeSpan.FromSeconds(10),
            RelayBaseUrl: server.BaseUri.ToString().TrimEnd('/'),
            State: "st");

        var captureTask = capture.CaptureAsync(options);
        var sessionCallback = await WaitForCallbackUrlAsync(progress);
        using var browser = new HttpClient();
        _ = await browser.GetAsync($"{sessionCallback}&error=access_denied&error_description=nope&state=st");

        var result = await captureTask;
        Assert.False(result.Ok);
        Assert.Equal("access_denied", result.Error);
        Assert.Equal("nope", result.ErrorDescription);
        Assert.Null(result.Code);
    }

    [Fact]
    public async Task Timeout_YieldsTimeoutError()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var http = new HttpClient();
        var capture = new HostedRelayCapture(http, TextWriter.Null, pollInterval: TimeSpan.FromMilliseconds(20));

        var options = new CaptureOptions(
            CallbackUrl: new Uri(server.BaseUri, "v1/callback"),
            Mode: CaptureMode.Hosted,
            Timeout: TimeSpan.FromMilliseconds(250),
            RelayBaseUrl: server.BaseUri.ToString().TrimEnd('/'));

        var result = await capture.CaptureAsync(options);
        Assert.False(result.Ok);
        Assert.Equal("timeout", result.Error);
        Assert.Equal(nameof(CaptureMode.Hosted), result.Mode);
    }

    [Fact]
    public async Task Cli_HostedMode_AgainstStub_ExitsZero()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var http = new HttpClient();
        var progress = new StringWriter();
        var capture = new HostedRelayCapture(http, progress, pollInterval: TimeSpan.FromMilliseconds(25));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var relayBase = server.BaseUri.ToString().TrimEnd('/');
        var runTask = OAuthCaptureCommand.RunAsync(
            callbackUrl: $"{relayBase}/v1/callback",
            mode: "hosted",
            timeoutSeconds: 10,
            relayBase: relayBase,
            state: "cli-state",
            stdout: stdout,
            stderr: stderr,
            hostedCapture: capture,
            isInteractive: false);

        var sessionCallback = await WaitForCallbackUrlAsync(progress);
        using var browser = new HttpClient();
        _ = await browser.GetAsync($"{sessionCallback}&code=CLI_HOSTED&state=cli-state");

        var exit = await runTask;
        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("\"mode\":\"Hosted\"", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("CLI_HOSTED", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_UnsupportedNonLoopbackHttp_ExitsUsageError()
    {
        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: "http://example.com/callback",
            mode: "auto",
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    private static async Task<Uri> WaitForCallbackUrlAsync(StringWriter progress)
    {
        for (var i = 0; i < 80; i++)
        {
            await Task.Delay(25);
            var text = progress.ToString();
            const string marker = "Hosted callback URL: ";
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                continue;
            }

            var line = text[(idx + marker.Length)..].Split('\n', '\r')[0].Trim();
            if (Uri.TryCreate(line, UriKind.Absolute, out var u))
            {
                return u;
            }
        }

        throw new TimeoutException("Hosted callback URL was not printed.");
    }
}
