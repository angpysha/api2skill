using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

public class CaptureModeInferenceTests
{
    [Theory]
    [InlineData("http://localhost:8400/callback", null, CaptureMode.HttpLoopback)]
    [InlineData("http://127.0.0.1:8400/callback", null, CaptureMode.HttpLoopback)]
    [InlineData("http://[::1]:8400/callback", null, CaptureMode.HttpLoopback)]
    [InlineData("https://localhost:8443/callback", null, CaptureMode.HttpsLoopback)]
    [InlineData("https://127.0.0.1:8443/cb", null, CaptureMode.HttpsLoopback)]
    [InlineData("api2skill://oauth/callback", null, CaptureMode.CustomScheme)]
    [InlineData("myapp://auth", null, CaptureMode.CustomScheme)]
    [InlineData("https://oauth.api2skill.dev/v1/callback", null, CaptureMode.Hosted)]
    public void Auto_InfersExpectedMode(string url, string? mode, CaptureMode expected)
    {
        var uri = new Uri(url);
        var resolved = CaptureModeResolver.Resolve(uri, mode);
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Auto_NonLoopbackHttp_IsUnsupported()
    {
        var resolved = CaptureModeResolver.Resolve(new Uri("http://example.com/callback"));
        Assert.Null(resolved);
    }

    [Fact]
    public void Auto_NonLoopbackHttps_UnknownHost_IsUnsupported()
    {
        var resolved = CaptureModeResolver.Resolve(new Uri("https://idp.example.com/callback"));
        Assert.Null(resolved);
    }

    [Theory]
    [InlineData("http", CaptureMode.HttpLoopback)]
    [InlineData("https", CaptureMode.HttpsLoopback)]
    [InlineData("scheme", CaptureMode.CustomScheme)]
    [InlineData("hosted", CaptureMode.Hosted)]
    public void ExplicitMode_OverridesInference(string mode, CaptureMode expected)
    {
        // Explicit mode wins even when URL would imply something else
        var resolved = CaptureModeResolver.Resolve(new Uri("http://localhost:1/"), mode);
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void UnknownModeToken_IsRejected()
    {
        Assert.False(CaptureModeResolver.IsKnownModeToken("bogus"));
        Assert.Null(CaptureModeResolver.Resolve(new Uri("http://localhost:1/"), "bogus"));
    }

    [Fact]
    public void RelayBaseOverride_MarksMatchingHostAsHosted()
    {
        var resolved = CaptureModeResolver.Resolve(
            new Uri("https://relay.example.test/v1/callback"),
            modeOverride: null,
            relayBaseUrl: "https://relay.example.test");
        Assert.Equal(CaptureMode.Hosted, resolved);
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("example.com", false)]
    public void IsLoopbackHost_MatchesKnownHosts(string host, bool expected) =>
        Assert.Equal(expected, CaptureModeResolver.IsLoopbackHost(host));
}
