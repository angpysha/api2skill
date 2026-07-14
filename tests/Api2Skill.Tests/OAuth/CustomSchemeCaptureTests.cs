using Api2Skill.Cli;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

public class CustomSchemeCaptureTests
{
    [Fact]
    public async Task Handoff_DeliversAuthorizationCode()
    {
        var callback = new Uri("api2skill://oauth/callback");
        var capture = new CustomSchemeCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10), State: "st");

        var captureTask = capture.CaptureAsync(options);
        await WaitForPipeAsync(callback.Scheme);

        var delivered = await CustomSchemeCapture.DeliverHandoffAsync(
            "api2skill://oauth/callback?code=SCHEME_CODE&state=st");
        Assert.Equal(0, delivered);

        var result = await captureTask;
        Assert.True(result.Ok);
        Assert.Equal(nameof(CaptureMode.CustomScheme), result.Mode);
        Assert.Equal("SCHEME_CODE", result.Code);
        Assert.Equal("st", result.State);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Handoff_IdpError_YieldsOkFalse()
    {
        var callback = new Uri("api2skill://oauth/callback");
        var capture = new CustomSchemeCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10));

        var captureTask = capture.CaptureAsync(options);
        await WaitForPipeAsync(callback.Scheme);

        _ = await CustomSchemeCapture.DeliverHandoffAsync(
            "api2skill://oauth/callback?error=access_denied&error_description=nope");

        var result = await captureTask;
        Assert.False(result.Ok);
        Assert.Equal("access_denied", result.Error);
        Assert.Equal("nope", result.ErrorDescription);
        Assert.Null(result.Code);
    }

    [Fact]
    public async Task Handoff_StateMismatch_YieldsError()
    {
        var callback = new Uri("api2skill://oauth/callback");
        var capture = new CustomSchemeCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromSeconds(10), State: "expected");

        var captureTask = capture.CaptureAsync(options);
        await WaitForPipeAsync(callback.Scheme);

        _ = await CustomSchemeCapture.DeliverHandoffAsync(
            "api2skill://oauth/callback?code=C&state=other");

        var result = await captureTask;
        Assert.False(result.Ok);
        Assert.Equal("state_mismatch", result.Error);
    }

    [Fact]
    public async Task Timeout_WhenNoHandoff()
    {
        var callback = new Uri($"a2stmout{Guid.NewGuid():N}"[..12] + "://cb");
        var capture = new CustomSchemeCapture();
        var options = new CaptureOptions(callback, Timeout: TimeSpan.FromMilliseconds(200));

        var result = await capture.CaptureAsync(options);
        Assert.False(result.Ok);
        Assert.Equal("timeout", result.Error);
        Assert.Equal(nameof(CaptureMode.CustomScheme), result.Mode);
    }

    [Fact]
    public async Task DeliverHandoff_NoWaiter_ReturnsNonZero()
    {
        var exit = await CustomSchemeCapture.DeliverHandoffAsync(
            $"a2snone{Guid.NewGuid():N}"[..12] + "://oauth/callback?code=x");
        Assert.NotEqual(0, exit);
    }

    [Fact]
    public async Task OAuthCapture_UnregisteredFirstParty_ExitsSix_WithHint()
    {
        var stderr = new StringWriter();
        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: "api2skill://oauth/callback",
            timeoutSeconds: 1,
            stdout: TextWriter.Null,
            stderr: stderr,
            isProtocolRegistered: _ => false,
            isInteractive: false);

        Assert.Equal(ExitCodes.CaptureTimeout, exit);
        Assert.Contains("register-protocol", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OAuthCapture_Registered_UsesSchemeCapture()
    {
        var stdout = new StringWriter();
        var fake = new FakeSchemeCapture();

        var exit = await OAuthCaptureCommand.RunAsync(
            callbackUrl: "api2skill://oauth/callback",
            timeoutSeconds: 5,
            stdout: stdout,
            stderr: TextWriter.Null,
            isProtocolRegistered: _ => true,
            schemeCapture: fake,
            isInteractive: false);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("\"code\":\"FAKE\"", stdout.ToString(), StringComparison.Ordinal);
    }

    private sealed class FakeSchemeCapture : IRedirectCapture
    {
        public Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CaptureResult(
                Ok: true,
                Mode: nameof(CaptureMode.CustomScheme),
                Code: "FAKE",
                State: null,
                Error: null,
                ErrorDescription: null,
                CallbackUrl: options.CallbackUrl.ToString()));
    }

    private static async Task WaitForPipeAsync(string scheme)
    {
        var pipeName = CustomSchemeCapture.PipeNameForScheme(scheme);
        for (var i = 0; i < 50; i++)
        {
            if (CustomSchemeCapture.IsPipeWaiting(pipeName))
            {
                return;
            }

            await Task.Delay(20);
        }
    }
}
