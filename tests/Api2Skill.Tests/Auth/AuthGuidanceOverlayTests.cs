using Api2Skill.Auth;
using Api2Skill.Model;
using Api2Skill.Output;

namespace Api2Skill.Tests.Auth;

public class AuthGuidanceOverlayTests
{
    [Fact]
    public void Apply_ReplacesGuidanceSchemes_KeepsProfiles()
    {
        var existing = """
            {
              "profiles": [ { "name": "oauth2", "type": "bearer", "token": "{secret:T}" } ],
              "_guidance": {
                "schemes": [ { "schemeId": "oauth2", "suggestedProfileName": "oauth2", "status": "scaffolded", "kind": "Bearer", "operations": [], "tags": [] } ],
                "manualOnlySchemes": []
              }
            }
            """;

        var scaffoldModel = new SkillModel(
            Name: "c",
            Title: "C",
            Description: null,
            Version: "1",
            BaseUrl: "https://api.example.com",
            SpecVersion: SpecVersionKind.OpenApi3_0,
            SecuritySchemes:
            [
                new SecuritySchemeModel("oauth2", SecuritySchemeKind.Bearer, "Authorization", null, null, [], ["oauth2.apiKey"]),
                new SecuritySchemeModel("api2_oauth2", SecuritySchemeKind.Bearer, "Authorization", null, null, [], ["api2_oauth2.apiKey"]),
            ],
            Tags:
            [
                new TagGroup("t", null, [
                    new OperationModel("op1", HttpMethod.Get, "/a", null, null, [], null, ["oauth2"], []),
                    new OperationModel("op2", HttpMethod.Get, "/b", null, null, [], null, ["api2_oauth2"], []),
                ]),
            ],
            Warnings: []);

        var scaffold = AuthScaffold.Build(scaffoldModel);
        var merged = AuthGuidanceOverlay.Apply(existing, scaffold.Json);

        Assert.Contains("\"name\": \"oauth2\"", merged, StringComparison.Ordinal);
        Assert.Contains("api2_oauth2", merged, StringComparison.Ordinal);
        Assert.Contains("_guidance", merged, StringComparison.Ordinal);
    }
}

public class SpecCacheTests
{
    [Fact]
    public void WriteAndResolveMergeSource_PrefersCache()
    {
        var dir = Path.Combine(Path.GetTempPath(), "api2skill-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var written = SpecCache.Write(dir, "api1", "json", "{\"openapi\":\"3.0.1\"}"u8.ToArray());
            Assert.True(File.Exists(written));

            var resolved = SpecCache.ResolveMergeSource(dir, "api1", "https://example.com/swagger.json");
            Assert.Equal(written, resolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
