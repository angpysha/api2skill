using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Api2Skill.Auth;
using Api2Skill.Cli;
using Api2Skill.Emit;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.Cli;

public class LoginCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-login-cmd-" + Guid.NewGuid().ToString("N"));

    public LoginCommandTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task MissingSkill_ExitsUsageError()
    {
        var exit = await LoginCommand.RunAsync(
            skillDir: null,
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task MissingAuthJson_ExitsAcquisitionFailure()
    {
        var exit = await LoginCommand.RunAsync(
            skillDir: _workDir,
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);
        Assert.Equal(ExitCodes.AcquisitionFailure, exit);
    }

    [Fact]
    public async Task Login_WithInjectedCapture_WritesAuthCache()
    {
        await WriteSkillAuthAsync(callbackUrl: "http://127.0.0.1:18400/callback");
        await File.WriteAllTextAsync(Path.Combine(_workDir, "secrets.json"), """{"CLIENT_ID":"cid"}""");

        var handler = new StubTokenHandler();
        string? capturedState = null;
        var exit = await LoginCommand.RunAsync(
            skillDir: _workDir,
            profile: "user",
            hooks: new SkillOAuthLogin.Hooks(
                CaptureAsync: (callback, state, _, _) =>
                {
                    capturedState = state;
                    return Task.FromResult(new CaptureResult(
                        Ok: true,
                        Mode: nameof(CaptureMode.HttpLoopback),
                        Code: "AUTHCODE",
                        State: state,
                        Error: null,
                        ErrorDescription: null,
                        CallbackUrl: callback.ToString()));
                },
                TokenHttpHandler: handler,
                TryLaunchBrowser: _ => false),
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(string.IsNullOrEmpty(capturedState));

        var cachePath = Path.Combine(_workDir, SecretsScaffold.TokenCacheFileName);
        Assert.True(File.Exists(cachePath));
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath));
        Assert.Equal("ACCESS", doc.RootElement.GetProperty("user").GetProperty("access_token").GetString());
        Assert.Equal("REFRESH", doc.RootElement.GetProperty("user").GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task Login_CaptureError_ExitsSeven()
    {
        await WriteSkillAuthAsync(callbackUrl: "http://127.0.0.1:18401/callback");
        await File.WriteAllTextAsync(Path.Combine(_workDir, "secrets.json"), """{"CLIENT_ID":"cid"}""");

        var exit = await LoginCommand.RunAsync(
            skillDir: _workDir,
            profile: "user",
            hooks: new SkillOAuthLogin.Hooks(
                CaptureAsync: (callback, state, _, _) => Task.FromResult(new CaptureResult(
                    Ok: false,
                    Mode: nameof(CaptureMode.HttpLoopback),
                    Code: null,
                    State: state,
                    Error: "access_denied",
                    ErrorDescription: "nope",
                    CallbackUrl: callback.ToString())),
                TryLaunchBrowser: _ => false),
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);

        Assert.Equal(ExitCodes.OAuthRedirectError, exit);
        Assert.False(File.Exists(Path.Combine(_workDir, SecretsScaffold.TokenCacheFileName)));
    }

    private async Task WriteSkillAuthAsync(string callbackUrl)
    {
        var auth = $$"""
            {
              "profiles": [
                {
                  "name": "user",
                  "type": "oauth2",
                  "grant": "authorization_code",
                  "authUrl": "https://idp.example/authorize",
                  "tokenUrl": "https://idp.example/token",
                  "callbackUrl": "{{callbackUrl}}",
                  "clientId": "{secret:CLIENT_ID}",
                  "scopes": ["openid"]
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(_workDir, "auth.json"), auth);
    }

    private sealed class StubTokenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = """{"access_token":"ACCESS","refresh_token":"REFRESH","expires_in":3600}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
