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
        Assert.Contains('\n', op.RequestBody.Schema.ExampleJson!); // indented pasteable JSON

        var created = Assert.Single(op.Responses, r => r.StatusCode == "201");
        Assert.Equal("application/json", created.ContentType);
        Assert.NotNull(created.Schema);
        Assert.Contains(created.Schema!.Properties, p => p.Name == "id" && p.Format == "uuid");
    }

    [Fact]
    public async Task NestedEnumsAndQueryParams_AreFullyMapped()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Nested", "version": "1" },
          "paths": {
            "/pets": {
              "get": {
                "operationId": "listPets",
                "parameters": [
                  {
                    "name": "status",
                    "in": "query",
                    "required": true,
                    "schema": { "type": "string", "enum": ["available", "sold"], "default": "available" }
                  }
                ],
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": {
                          "type": "object",
                          "properties": {
                            "pet": {
                              "type": "object",
                              "properties": {
                                "name": { "type": "string" },
                                "tag": {
                                  "type": "object",
                                  "properties": {
                                    "id": { "type": "integer", "format": "int64" }
                                  }
                                }
                              }
                            }
                          }
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
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "nested"));
        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));

        var status = Assert.Single(op.Parameters);
        Assert.Equal("status", status.Name);
        Assert.Equal(ParameterLocation.Query, status.In);
        Assert.Equal(["available", "sold"], status.EnumValues);
        Assert.Equal("available", status.Example);

        var ok = Assert.Single(op.Responses, r => r.StatusCode == "200");
        Assert.Contains(ok.Schema!.Properties, p => p.Name == "pet");
        Assert.Contains(ok.Schema.Properties, p => p.Name == "pet.tag");
        Assert.Contains(ok.Schema.Properties, p => p.Name == "pet.tag.id" && p.Format == "int64");
    }

    [Fact]
    public async Task RefBodies_SetSchemaName_AndComponentSchemasIncludeRawJson()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Refs", "version": "1" },
          "paths": {
            "/pets": {
              "post": {
                "operationId": "addPet",
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": {
                      "schema": { "$ref": "#/components/schemas/PetInput" }
                    }
                  }
                },
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": {
                      "application/json": {
                        "schema": { "$ref": "#/components/schemas/Pet" }
                      }
                    }
                  }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "Category": {
                "type": "object",
                "properties": {
                  "id": { "type": "integer" },
                  "name": { "type": "string" }
                }
              },
              "PetInput": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "category": { "$ref": "#/components/schemas/Category" }
                }
              },
              "Pet": {
                "type": "object",
                "properties": {
                  "id": { "type": "integer" },
                  "name": { "type": "string" },
                  "category": { "$ref": "#/components/schemas/Category" }
                }
              },
              "Unused": { "type": "object", "properties": { "x": { "type": "string" } } }
            }
          }
        }
        """;

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "refs"));
        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));

        Assert.Equal("PetInput", op.RequestBody!.SchemaName);
        Assert.Equal("PetInput", op.RequestBody.Schema!.SchemaName);
        Assert.Equal("Pet", Assert.Single(op.Responses).SchemaName);

        Assert.Contains(model.ComponentSchemas, s => s.Name == "Pet");
        Assert.Contains(model.ComponentSchemas, s => s.Name == "PetInput");
        Assert.Contains(model.ComponentSchemas, s => s.Name == "Category");
        Assert.DoesNotContain(model.ComponentSchemas, s => s.Name == "Unused");

        var pet = model.ComponentSchemas.Single(s => s.Name == "Pet");
        Assert.Contains("\"properties\"", pet.RawJson);
        Assert.Contains("#/components/schemas/Category", pet.RawJson);
    }

    [Fact]
    public async Task DepthFourNesting_SetsTruncatedFlag()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Deep", "version": "1" },
          "paths": {
            "/deep": {
              "post": {
                "operationId": "deep",
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": {
                        "type": "object",
                        "properties": {
                          "l1": {
                            "type": "object",
                            "properties": {
                              "l2": {
                                "type": "object",
                                "properties": {
                                  "l3": {
                                    "type": "object",
                                    "properties": {
                                      "l4": {
                                        "type": "object",
                                        "properties": {
                                          "l5": { "type": "string" }
                                        }
                                      }
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                },
                "responses": { "200": { "description": "ok" } }
              }
            }
          }
        }
        """;

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "deep"));
        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));

        Assert.True(op.RequestBody!.Schema!.Truncated);
        Assert.Contains(op.RequestBody.Schema.Properties, p => p.Name == "l1.l2.l3.l4");
        Assert.DoesNotContain(op.RequestBody.Schema.Properties, p => p.Name.Contains("l5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OneOf_ExposesVariants_AndUsesFirstForExample()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "OneOf", "version": "1" },
          "paths": {
            "/x": {
              "post": {
                "operationId": "postX",
                "requestBody": {
                  "content": {
                    "application/json": {
                      "schema": {
                        "oneOf": [
                          { "type": "object", "properties": { "a": { "type": "string" } } },
                          { "$ref": "#/components/schemas/Alt" }
                        ]
                      }
                    }
                  }
                },
                "responses": { "200": { "description": "ok" } }
              }
            }
          },
          "components": {
            "schemas": {
              "Alt": { "type": "object", "properties": { "b": { "type": "integer" } } }
            }
          }
        }
        """;

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "oneof"));
        var op = Assert.Single(model.Tags.SelectMany(t => t.Operations));

        Assert.NotNull(op.RequestBody!.Schema!.Variants);
        Assert.Equal(2, op.RequestBody.Schema.Variants!.Count);
        Assert.Equal("Alt", op.RequestBody.Schema.Variants[1].SchemaName);
        Assert.Contains("\"a\"", op.RequestBody.Schema.ExampleJson);
        Assert.Contains(model.ComponentSchemas, s => s.Name == "Alt");
    }

    [Fact]
    public async Task IncludeFilter_OnlyEmitsReachableComponentSchemas()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");

        var storeOnly = SkillModelBuilder.Build(
            loaded.Document,
            loaded.SpecVersion,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["tag:store"]));
        Assert.Empty(storeOnly.ComponentSchemas ?? []);

        var petOnly = SkillModelBuilder.Build(
            loaded.Document,
            loaded.SpecVersion,
            new BuildOptions(Name: "petstore", IncludeSelectors: ["tag:pet"]));
        Assert.Contains(petOnly.ComponentSchemas, s => s.Name == "Pet");
        Assert.Contains(petOnly.ComponentSchemas, s => s.Name == "PetInput");
        Assert.Contains(petOnly.ComponentSchemas, s => s.Name == "Category");
        Assert.Contains(petOnly.ComponentSchemas, s => s.Name == "Tag");
    }
}
