using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Model;

/// <summary>
/// T036: <c>--include</c>/<c>--exclude</c> selectors (tag:/path:/op:) and the requirement that
/// filtering recomputes which security schemes actually appear in the model (FR-004b). The
/// filter logic itself lives in SkillModelBuilder — it was built opportunistically during
/// Foundational (natural to add alongside tag grouping) rather than deferred to this story, so
/// these tests are new coverage for already-shipped behavior, not TDD-first for new code.
/// </summary>
public class FilterTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<Microsoft.OpenApi.OpenApiDocument> LoadPetstoreDocumentAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        return loaded.Document;
    }

    private static async Task<Microsoft.OpenApi.OpenApiDocument> LoadMultiAuthDocumentAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        return loaded.Document;
    }

    [Fact]
    public async Task Include_ByTag_KeepsOnlyMatchingTag()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["tag:store"]));

        Assert.Equal(["store"], model.Tags.Select(t => t.Tag));
        Assert.Single(model.Tags.Single().Operations);
    }

    [Fact]
    public async Task Include_ByOperationId_KeepsOnlyThatOperation()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["op:addPet"]));

        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));
        Assert.Equal("addPet", op.OperationId);
    }

    [Fact]
    public async Task Include_ByPathGlob_KeepsMatchingPaths()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["path:/pet*"]));

        var paths = model.Tags.SelectMany(t => t.Operations).Select(o => o.PathTemplate).OrderBy(p => p, StringComparer.Ordinal);
        Assert.Equal(["/pet", "/pet/{petId}"], paths);
    }

    [Fact]
    public async Task Exclude_ByTag_DropsMatchingTag_AppliedAfterInclude()
    {
        var doc = await LoadPetstoreDocumentAsync();
        // Include everything, then exclude "default" — proves exclude is applied after include,
        // not just an alternative to it.
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", ExcludeSelectors: ["tag:default"]));

        Assert.DoesNotContain("default", model.Tags.Select(t => t.Tag));
        Assert.Equal(3, model.Tags.Sum(t => t.Operations.Count)); // 4 total - 1 default (get_health)
    }

    [Fact]
    public async Task IncludeAndExclude_Combined_ExcludeWinsWithinTheIncludedSet()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["tag:pet"], ExcludeSelectors: ["op:addPet"]));

        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));
        Assert.Equal("getPetById", op.OperationId);
    }

    [Fact]
    public async Task Filtering_RecomputesSecuritySchemes_DroppingSchemesNoLongerReferenced()
    {
        var doc = await LoadMultiAuthDocumentAsync();

        var unfiltered = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "multi-auth"));
        Assert.True(unfiltered.SecuritySchemes.Count >= 4);

        // Keep only the apiKey operation — every other scheme must disappear from the model,
        // proving SecuritySchemes is recomputed from the *filtered* operations, not the
        // full spec (FR-004b).
        var filtered = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "multi-auth", IncludeSelectors: ["op:apiKeyOp"]));

        Assert.Equal(["apiKeyAuth"], filtered.SecuritySchemes.Select(s => s.Id));
    }

    [Fact]
    public async Task Include_SelectorWithNoMatches_ProducesEmptyModel_NotAnError()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["tag:does-not-exist"]));

        Assert.Empty(model.Tags);
        Assert.Contains(model.Warnings, w => w.Contains("no callable operations"));
    }
}
