using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Model;

public class SchemaDetailMappingTests
{
    [Fact]
    public async Task RequestAndResponseBodies_IncludePropertyTablesAndExamples()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Bodies", "version": "1" },
          "paths": {
            "/items": {
              "post": {
                "operationId": "createItem",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "required": ["name"],
                        "properties": {
                          "name": { "type": "string", "description": "Item name" },
                          "qty": { "type": "integer", "format": "int32" }
                        }
                      }
                    }
                  }
                },
                "responses": {
                  "201": {
                    "description": "created",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "id": { "type": "string", "format": "uuid" }
                          },
                          "required": ["id"]
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "bodies"));
        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));

        Assert.NotNull(op.RequestBody);
        Assert.NotNull(op.RequestBody!.Schema);
        Assert.Contains(op.RequestBody.Schema!.Properties, p => p.Name == "name" && p.Required);
        Assert.Contains("\"name\": \"string\"", op.RequestBody.Schema.ExampleJson);

        var created = Assert.Single(op.Responses, r => r.StatusCode == "201");
        Assert.Equal("application/json", created.ContentType);
        Assert.NotNull(created.Schema);
        Assert.Contains(created.Schema!.Properties, p => p.Name == "id" && p.Format == "uuid");
    }
}
