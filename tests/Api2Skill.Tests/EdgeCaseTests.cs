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
    public async Task SamePath_DifferentHttpMethods_AreDistinctOperations_NotPathOnlyDuplicates()
    {
        // OpenAPI allows multiple HTTP methods on one path. Uniqueness must consider method+path
        // (EC-3), not path alone — otherwise GET /items and POST /items would be treated as
        // duplicates and the second operation would be dropped or mis-disambiguated.
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Same Path Methods", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/items": {
              "get": { "responses": { "200": { "description": "ok" } } },
              "post": { "responses": { "201": { "description": "created" } } }
            }
          }
        }
        """;
        var doc = await ParseAsync(json);
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "same-path-methods"));

        var ops = model.Tags.SelectMany(t => t.Operations).ToList();
        Assert.Equal(2, ops.Count);
        Assert.Equal(2, ops.Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count());

        var byMethod = ops.ToDictionary(o => o.Method.Method, o => o, StringComparer.Ordinal);
        Assert.Equal("GET", byMethod["GET"].Method.Method);
        Assert.Equal("POST", byMethod["POST"].Method.Method);
        Assert.Equal("/items", byMethod["GET"].PathTemplate);
        Assert.Equal("/items", byMethod["POST"].PathTemplate);
        Assert.Equal("get_items", byMethod["GET"].OperationId);
        Assert.Equal("post_items", byMethod["POST"].OperationId);
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

    [Fact]
    public async Task ExplicitlyDuplicatedOperationId_IsDisambiguatedTheSameWayAsASynthesizedCollision()
    {
        // EC-5 covers colliding *synthesized* ids; this covers the author declaring the same
        // literal operationId twice by hand (a genuinely invalid-but-common-in-the-wild spec) —
        // the dispatcher must still be able to resolve both operations unambiguously.
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Duplicate Explicit Ids", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/a": { "get": { "operationId": "dup", "responses": { "200": { "description": "ok" } } } },
            "/b": { "get": { "operationId": "dup", "responses": { "200": { "description": "ok" } } } }
          }
        }
        """;
        var doc = await ParseAsync(json);
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "dup-explicit"));

        var ids = model.Tags.SelectMany(t => t.Operations).Select(o => o.OperationId).ToList();

        Assert.Equal(2, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("dup", ids);
        Assert.Contains("dup_2", ids);
    }

    [Fact]
    public async Task CircularSchemaReference_DoesNotHangOrCrashDuringBuild()
    {
        // A self-referencing schema (a "Node" with a "children" array of itself and a "parent"
        // pointing back to itself) is legal OpenAPI. DescribeSchema depth-caps and NoteSchemaGraph
        // tracks visited component names, so this must complete promptly rather than looping forever
        // walking the reference graph.
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Circular Refs", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/node": {
              "post": {
                "operationId": "createNode",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": { "schema": { "$ref": "#/components/schemas/Node" } }
                  }
                },
                "responses": { "200": { "description": "ok" } }
              }
            }
          },
          "components": {
            "schemas": {
              "Node": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "children": { "type": "array", "items": { "$ref": "#/components/schemas/Node" } },
                  "parent": { "$ref": "#/components/schemas/Node" }
                }
              }
            }
          }
        }
        """;
        var doc = await ParseAsync(json);

        var buildTask = Task.Run(() => SkillModelBuilder.Build(
            doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0, new BuildOptions(Name: "circular")));
        var completed = await Task.WhenAny(buildTask, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(buildTask, completed);
        var model = await buildTask;
        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));
        Assert.NotNull(op.RequestBody);
        Assert.Equal("Node", op.RequestBody!.SchemaName);
        Assert.Contains("object", op.RequestBody.SchemaSummary);
        Assert.Contains(model.ComponentSchemas, s => s.Name == "Node" && s.RawJson.Contains("#/components/schemas/Node"));
    }

    [Fact]
    public async Task NonStringEnumValues_OnAQueryParameter_DoNotCrashMapping()
    {
        // An integer-valued enum (e.g. a numeric status code parameter) is legal OpenAPI;
        // ParameterModel records the schema type plus stringified enum values for reference docs.
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Non-String Enum", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/items": {
              "get": {
                "operationId": "listItems",
                "parameters": [
                  { "name": "status", "in": "query", "schema": { "type": "integer", "enum": [1, 2, 3] } }
                ],
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;
        var doc = await ParseAsync(json);
        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "non-string-enum"));

        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));
        var param = Assert.Single(op.Parameters);
        Assert.Equal("status", param.Name);
        Assert.Equal("Integer", param.Type);
        Assert.Equal(["1", "2", "3"], param.EnumValues);
    }

    [Fact]
    public async Task DeeplyNestedSchema_DoesNotStackOverflowOrHang()
    {
        // Nested levels of `{"type":"object","properties":{"child": <nested>}}` —
        // SummarizeSchema must not attempt to walk this recursively (it only reads the top
        // level), but this guards the whole parse->build pipeline against a pathological spec
        // regardless. Kept comfortably under System.Text.Json's default 64-level reader depth
        // (the surrounding paths/requestBody/content wrapper adds a handful of levels on top of
        // this) — a spec deep enough to hit *that* limit already fails cleanly as a parse error
        // (FR-010), which is a distinct, already-covered scenario (ExitCodeTests), not a hang.
        var nested = "{\"type\":\"string\"}";
        for (var i = 0; i < 20; i++)
        {
            nested = "{\"type\":\"object\",\"properties\":{\"child\":" + nested + "}}";
        }
        var json = "{"
            + "\"openapi\":\"3.0.3\","
            + "\"info\":{\"title\":\"Deep Nesting\",\"version\":\"1\"},"
            + "\"servers\":[{\"url\":\"https://example.com\"}],"
            + "\"paths\":{\"/deep\":{\"post\":{"
            + "\"operationId\":\"postDeep\","
            + "\"requestBody\":{\"content\":{\"application/json\":{\"schema\":" + nested + "}}},"
            + "\"responses\":{\"200\":{\"description\":\"ok\"}}"
            + "}}}"
            + "}";
        var doc = await ParseAsync(json);

        var buildTask = Task.Run(() => SkillModelBuilder.Build(
            doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0, new BuildOptions(Name: "deep-nesting")));
        var completed = await Task.WhenAny(buildTask, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(buildTask, completed);
        var model = await buildTask;
        Assert.Single(model.Tags.SelectMany(t => t.Operations));
    }
}
