using Api2Skill.Auth;
using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Auth;

/// <summary>
/// T005/T006/T015: scheme→profile mapping, loader validation, naming guidance, and no literal secrets.
/// </summary>
public class AuthScaffoldTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<SkillModel> BuildMultiAuthModelAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        return SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));
    }

    [Fact]
    public async Task Build_MapsSupportedSchemesToActiveProfiles()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        var config = AuthConfigLoader.Parse(StripScaffoldMetadata(result.Json));
        var names = config.Profiles.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(["apiKeyAuth", "basicAuth", "bearerAuth", "oauth2Auth"], names);

        Assert.Equal(AuthType.Bearer, config.Profiles.Single(p => p.Name == "bearerAuth").Type);
        Assert.Equal(AuthType.Basic, config.Profiles.Single(p => p.Name == "basicAuth").Type);
        Assert.Equal(AuthType.Custom, config.Profiles.Single(p => p.Name == "apiKeyAuth").Type);
        Assert.Equal(AuthType.OAuth2, config.Profiles.Single(p => p.Name == "oauth2Auth").Type);
        Assert.Equal(OAuthGrant.ClientCredentials, config.Profiles.Single(p => p.Name == "oauth2Auth").OAuth!.Grant);
    }

    [Fact]
    public async Task Build_OmitsUnsupportedAndListsInGuidance()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        var config = AuthConfigLoader.Parse(StripScaffoldMetadata(result.Json));
        Assert.DoesNotContain(config.Profiles, p => p.Name == "openIdAuth");
        var openId = result.Guidance.Schemes.Single(s => s.SchemeId == "openIdAuth");
        Assert.Equal(SchemeScaffoldStatus.ManualOnly, openId.Status);
        Assert.Contains("openIdAuth", result.Json, StringComparison.Ordinal); // in _guidance.manualOnlySchemes
    }

    [Fact]
    public async Task Build_ProfileNamesEqualSchemeIds()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        foreach (var entry in result.Guidance.Schemes)
        {
            Assert.Equal(entry.SchemeId, entry.SuggestedProfileName);
        }
    }

    [Fact]
    public async Task Build_ContainsOnlySecretPlaceholders()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        Assert.DoesNotContain("SUPER-SECRET", result.Json, StringComparison.Ordinal);
        Assert.Contains("{secret:", result.Json, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"""[A-Za-z0-9+/]{32,}""", result.Json);
    }

    [Fact]
    public async Task Build_ActiveProfilesPassAuthConfigLoader()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        var config = AuthConfigLoader.Parse(StripScaffoldMetadata(result.Json));
        Assert.True(config.Profiles.Count > 0);
    }

    [Fact]
    public async Task Build_SharedSchemeIdAppearsOnceInGuidanceWithAggregatedOperations()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        var bearerEntries = result.Guidance.Schemes.Where(s => s.SchemeId == "bearerAuth").ToList();
        Assert.Single(bearerEntries);
        Assert.Equal(["bearerOp"], bearerEntries[0].OperationIds);
        Assert.Equal(["auth-samples"], bearerEntries[0].Tags);
    }

    [Fact]
    public async Task Build_IncludesTagAttachExamplesWhenTagsExist()
    {
        var model = await BuildMultiAuthModelAsync();
        var result = AuthScaffold.Build(model);

        Assert.Contains("_tagAttachExamples", result.Json, StringComparison.Ordinal);
        Assert.NotEmpty(result.Guidance.TagAttachExamples);
        Assert.All(result.Guidance.TagAttachExamples, e => Assert.Equal("auth-samples", e.Tag));
    }

    private static string StripScaffoldMetadata(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("profiles");
            doc.RootElement.GetProperty("profiles").WriteTo(writer);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
