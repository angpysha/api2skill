using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

public class EmitterOperationsTests
{
    [Fact]
    public async Task DistinctByOperationId_SamePathDifferentMethods_EmitsBothOperations()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Same Path Methods", "version": "1" },
          "servers": [{ "url": "https://example.com" }],
          "paths": {
            "/items": {
              "get": { "tags": ["a", "b"], "responses": { "200": { "description": "ok" } } },
              "post": { "tags": ["a", "b"], "responses": { "201": { "description": "created" } } }
            }
          }
        }
        """;
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "emit"));

        var distinct = EmitterOperations.DistinctByOperationId(model).ToList();
        Assert.Equal(2, distinct.Count);
        Assert.Contains(distinct, o => o.Method.Method == "GET" && o.PathTemplate == "/items");
        Assert.Contains(distinct, o => o.Method.Method == "POST" && o.PathTemplate == "/items");
    }
}
