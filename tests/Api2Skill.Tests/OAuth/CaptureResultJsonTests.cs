using System.Text.Json;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

public class CaptureResultJsonTests
{
    [Fact]
    public void Success_RoundTrips_WithContractFieldNames()
    {
        var original = new CaptureResult(
            Ok: true,
            Mode: nameof(CaptureMode.HttpLoopback),
            Code: "AUTHORIZATION_CODE",
            State: "STATE_VALUE",
            Error: null,
            ErrorDescription: null,
            CallbackUrl: "http://localhost:8400/callback");

        var json = JsonSerializer.Serialize(original, CaptureResultJsonContext.Default.CaptureResult);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("HttpLoopback", root.GetProperty("mode").GetString());
        Assert.Equal("AUTHORIZATION_CODE", root.GetProperty("code").GetString());
        Assert.Equal("STATE_VALUE", root.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("error").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("errorDescription").ValueKind);
        Assert.Equal("http://localhost:8400/callback", root.GetProperty("callbackUrl").GetString());

        var back = JsonSerializer.Deserialize(json, CaptureResultJsonContext.Default.CaptureResult);
        Assert.NotNull(back);
        Assert.Equal(original, back);
    }

    [Fact]
    public void Failure_Timeout_RoundTrips()
    {
        var original = new CaptureResult(
            Ok: false,
            Mode: nameof(CaptureMode.Hosted),
            Code: null,
            State: null,
            Error: "timeout",
            ErrorDescription: "No redirect received within 180 seconds",
            CallbackUrl: "https://oauth.api2skill.dev/v1/callback");

        var json = JsonSerializer.Serialize(original, CaptureResultJsonContext.Default.CaptureResult);
        var back = JsonSerializer.Deserialize(json, CaptureResultJsonContext.Default.CaptureResult);
        Assert.Equal(original, back);
        Assert.Contains("\"error\":\"timeout\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Failure_IdpError_RoundTrips()
    {
        var original = new CaptureResult(
            Ok: false,
            Mode: nameof(CaptureMode.HttpsLoopback),
            Code: null,
            State: "STATE_VALUE",
            Error: "access_denied",
            ErrorDescription: "user cancelled",
            CallbackUrl: "https://localhost:8400/callback");

        var json = JsonSerializer.Serialize(original, CaptureResultJsonContext.Default.CaptureResult);
        var back = JsonSerializer.Deserialize(json, CaptureResultJsonContext.Default.CaptureResult);
        Assert.Equal(original, back);
        Assert.Contains("errorDescription", json, StringComparison.Ordinal);
    }
}
