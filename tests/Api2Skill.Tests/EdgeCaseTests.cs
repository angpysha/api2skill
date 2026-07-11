using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests;

/// <summary>
/// T042: edge cases from spec.md not already covered by a more specific test file —
/// EC-3/EC-4 (id synthesis, default tag) and EC-6 (unsupported-scheme warning) are covered in
/// SkillModelBuilderTests; EC-1/EC-8/EC-10 are covered in ExitCodeTests/UrlFetchTlsTests.
/// </summary>
public class EdgeCaseTests
{
    private static async Task<Microsoft.OpenApi.OpenApiDocument> ParseAsync(string json)
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        return loaded.Document;
    }

    [Fact]
    public async Task EC5_CollidingSynthesizedOperationIds_AreDisambiguatedDeterministically()
    {
        // Two GET operations whose paths sanitize to the SAME synthesized id
        // ("get_pet_{id}" vs "get_pet_{name}" both -> "get_pet_id"/"get_pet_name" — pick two
        // that genuinely collide once non-alphanumerics are collapsed to '_').
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Collision Test", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/pet/{id}": { "get": { "responses": { "200": { "description": "ok" } } } },
            "/pet-id": { "get": { "responses": { "200": { "description": "ok" } } } }
          }
        }
        """;
        var doc = await ParseAsync(json);
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "collision"));

        var ids = model.Tags.SelectMany(t => t.Operations).Select(o => o.OperationId).ToList();

        // Both paths sanitize to "get_pet_id" — the dispatcher must be able to resolve either
        // unambiguously, so the ids actually generated must be distinct.
        Assert.Equal(2, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("get_pet_id", ids);
        Assert.Contains("get_pet_id_2", ids);
    }

    [Fact]
    public async Task EC5_CollisionDisambiguation_IsStableAcrossRuns()
    {
        // Same input, parsed and built twice — the *same* path must get the *same* suffix both
        // times (paths.GetEnumerator() order is document order for Microsoft.OpenApi, which is
        // what makes this deterministic rather than incidental).
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Collision Test", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/pet/{id}": { "get": { "responses": { "200": { "description": "ok" } } } },
            "/pet-id": { "get": { "responses": { "200": { "description": "ok" } } } }
          }
        }
        """;

        var ids1 = model_ids(await ParseAsync(json));
        var ids2 = model_ids(await ParseAsync(json));
        Assert.Equal(ids1, ids2);

        static List<string> model_ids(Microsoft.OpenApi.OpenApiDocument doc) =>
            SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0, new BuildOptions(Name: "x"))
                .Tags.SelectMany(t => t.Operations).Select(o => o.OperationId).ToList();
    }

    [Fact]
    public async Task EC7_NoServersInSpec_LeavesBaseUrlNull_AndWarns()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "No Servers", "version": "1" },
          "paths": {
            "/ping": { "get": { "operationId": "ping", "responses": { "200": { "description": "ok" } } } }
          }
        }
        """;
        var doc = await ParseAsync(json);
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "no-servers"));

        Assert.Null(model.BaseUrl);
        Assert.Contains(model.Warnings, w => w.Contains("no `servers` entry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EC7_BaseUrlOverride_SatisfiesTheNoServersCase_WithoutAWarning()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "No Servers", "version": "1" },
          "paths": {
            "/ping": { "get": { "operationId": "ping", "responses": { "200": { "description": "ok" } } } }
          }
        }
        """;
        var doc = await ParseAsync(json);
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "no-servers", BaseUrlOverride: "https://override.example.com"));

        Assert.Equal("https://override.example.com", model.BaseUrl);
        Assert.DoesNotContain(model.Warnings, w => w.Contains("no `servers` entry", StringComparison.Ordinal));
    }
}
