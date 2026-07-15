using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Parsing;

public class SkillModelBuilderTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<SkillModel> BuildFromFixtureAsync(string fixtureName, string format)
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath(fixtureName)));
        var loaded = await OpenApiLoader.LoadAsync(stream, format);
        return SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));
    }

    [Theory]
    [InlineData("petstore.json", "json")]
    [InlineData("petstore.yaml", "yaml")]
    public async Task Build_MapsOperationsTagsAndServers_ForBothInputFormats(string fixture, string format)
    {
        var model = await BuildFromFixtureAsync(fixture, format);

        Assert.Equal("Swagger Petstore", model.Title);
        Assert.Equal("https://petstore.example.com/v2", model.BaseUrl);
        Assert.Equal(SpecVersionKind.OpenApi3_0, model.SpecVersion);

        var totalOps = model.Tags.Sum(t => t.Operations.Count);
        Assert.Equal(5, totalOps);

        // Tags: pet (declared, 3 ops), store (declared, 1 op), default (undeclared tag, 1 op — EC-4).
        Assert.Equal(["pet", "store", "default"], model.Tags.Select(t => t.Tag));
        Assert.Equal(3, model.Tags.Single(t => t.Tag == "pet").Operations.Count);
        Assert.Single(model.Tags.Single(t => t.Tag == "store").Operations);
        Assert.Single(model.Tags.Single(t => t.Tag == "default").Operations);
    }

    [Theory]
    [InlineData("petstore.json", "json")]
    [InlineData("petstore.yaml", "yaml")]
    public async Task Build_SynthesizesOperationId_WhenMissing(string fixture, string format)
    {
        var model = await BuildFromFixtureAsync(fixture, format);

        var storeOp = model.Tags.Single(t => t.Tag == "store").Operations.Single();
        Assert.Equal("get_store_inventory", storeOp.OperationId);

        var defaultOp = model.Tags.Single(t => t.Tag == "default").Operations.Single();
        Assert.Equal("get_health", defaultOp.OperationId);
        Assert.Empty(defaultOp.SecuritySchemeIds);
    }

    [Theory]
    [InlineData("petstore.json", "json")]
    [InlineData("petstore.yaml", "yaml")]
    public async Task Build_MapsPathParameterAndApiKeyAuth_ForGetPetById(string fixture, string format)
    {
        var model = await BuildFromFixtureAsync(fixture, format);

        var op = model.Tags.Single(t => t.Tag == "pet").Operations.Single(o => o.OperationId == "getPetById");

        Assert.Equal(HttpMethod.Get, op.Method);
        Assert.Equal("/pet/{petId}", op.PathTemplate);

        var param = Assert.Single(op.Parameters);
        Assert.Equal("petId", param.Name);
        Assert.Equal(ParameterLocation.Path, param.In);
        Assert.True(param.Required);

        Assert.Equal(["api_key"], op.SecuritySchemeIds);

        var scheme = model.SecuritySchemes.Single(s => s.Id == "api_key");
        Assert.Equal(SecuritySchemeKind.ApiKey, scheme.Kind);
        Assert.Equal(["apiKey"], scheme.SecretKeys);
    }

    [Theory]
    [InlineData("petstore.json", "json")]
    [InlineData("petstore.yaml", "yaml")]
    public async Task Build_MapsRequestBodyAndOAuth2Auth_ForAddPet(string fixture, string format)
    {
        var model = await BuildFromFixtureAsync(fixture, format);

        var op = model.Tags.Single(t => t.Tag == "pet").Operations.Single(o => o.OperationId == "addPet");

        Assert.NotNull(op.RequestBody);
        Assert.Equal("application/json", op.RequestBody!.ContentType);
        Assert.True(op.RequestBody.Required);

        Assert.Equal(["petstore_auth"], op.SecuritySchemeIds);

        var scheme = model.SecuritySchemes.Single(s => s.Id == "petstore_auth");
        Assert.Equal(SecuritySchemeKind.OAuth2, scheme.Kind);
        Assert.Equal("https://petstore.example.com/oauth/token", scheme.OAuthTokenUrl);
        Assert.Equal(["clientId", "clientSecret", "tokenUrl"], scheme.SecretKeys);
    }

    [Fact]
    public async Task Build_MapsAllFourSchemesAndWarnsOnUnsupported()
    {
        var model = await BuildFromFixtureAsync("multi-auth.yaml", "yaml");

        Assert.Equal(6, model.Tags.Sum(t => t.Operations.Count));

        var kinds = model.SecuritySchemes.ToDictionary(s => s.Id, s => s.Kind);
        Assert.Equal(SecuritySchemeKind.ApiKey, kinds["apiKeyAuth"]);
        Assert.Equal(SecuritySchemeKind.Bearer, kinds["bearerAuth"]);
        Assert.Equal(SecuritySchemeKind.Basic, kinds["basicAuth"]);
        Assert.Equal(SecuritySchemeKind.OAuth2, kinds["oauth2Auth"]);
        Assert.Equal(SecuritySchemeKind.Unsupported, kinds["openIdAuth"]);

        Assert.Contains(model.Warnings, w => w.Contains("openIdAuth") && w.Contains("unsupported type"));
    }

    [Fact]
    public async Task Build_AppliesIncludeFilter_ByTag()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");

        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["tag:pet"]));

        Assert.Equal(["pet"], model.Tags.Select(t => t.Tag));
        Assert.Equal(3, model.Tags.Single().Operations.Count);
    }
}
