using System.Net;
using System.Text;
using Api2Skill.Auth;
using Api2Skill.Cli;

namespace Api2Skill.Tests.Auth;

public class SkillOAuthLoginClientCredentialsTests : IDisposable
{
    private readonly string _skillDir = Path.Combine(Path.GetTempPath(), "api2skill-login-cc-" + Guid.NewGuid().ToString("N"));

    public SkillOAuthLoginClientCredentialsTests()
    {
        Directory.CreateDirectory(_skillDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_skillDir))
        {
            Directory.Delete(_skillDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ClientCredentials_WritesAccessTokenToCache()
    {
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "auth.json"), """
            { "profiles": [
              { "name": "svc", "type": "oauth2", "grant": "client_credentials",
                "clientId": "cid", "clientSecret": "sec", "tokenUrl": "https://example.com/token" }
            ] }
            """);
        var handler = new RecordingHandler("""{"access_token":"SERVICE-TOKEN","expires_in":3600}""");

        var exit = await SkillOAuthLogin.RunAsync(
            new SkillOAuthLogin.RunOptions(_skillDir, ProfileName: "svc"),
            hooks: new SkillOAuthLogin.Hooks(TokenHttpHandler: handler));

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("grant_type=client_credentials", handler.Body, StringComparison.Ordinal);
        Assert.Contains("client_id=cid", handler.Body, StringComparison.Ordinal);
        Assert.Contains("client_secret=sec", handler.Body, StringComparison.Ordinal);
        var cache = await File.ReadAllTextAsync(Path.Combine(_skillDir, ".auth-cache.json"));
        Assert.Contains("SERVICE-TOKEN", cache, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
