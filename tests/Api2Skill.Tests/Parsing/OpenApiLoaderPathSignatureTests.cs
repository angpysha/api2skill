using Api2Skill.Cli;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Parsing;

/// <summary>
/// OpenAPI.NET treats paths with the same "signature" (parameter names normalized to <c>{}</c>)
/// as duplicates. Many real specs use different parameter names for the same route shape.
/// </summary>
public class OpenApiLoaderPathSignatureTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-pathsig-" + Guid.NewGuid().ToString("N"));

    public OpenApiLoaderPathSignatureTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static async Task<LoadedSpec> LoadAsync(string json)
    {
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return await OpenApiLoader.LoadAsync(stream, "json");
    }

    [Fact]
    public async Task SamePathSignature_DifferentParameterNames_ParsesBothPathEntries()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Forms", "version": "1" },
          "servers": [{ "url": "https://api.example.com" }],
          "paths": {
            "/v1/forms/{id}": {
              "get": { "operationId": "getForm", "responses": { "200": { "description": "ok" } } }
            },
            "/v1/forms/{formId}": {
              "put": { "operationId": "updateForm", "responses": { "200": { "description": "ok" } } }
            }
          }
        }
        """;

        var loaded = await LoadAsync(json);

        Assert.Equal(2, loaded.Document.Paths.Count);
        Assert.True(loaded.Document.Paths.ContainsKey("/v1/forms/{id}"));
        Assert.True(loaded.Document.Paths.ContainsKey("/v1/forms/{formId}"));
        Assert.Contains(loaded.Warnings, w => w.Contains("path signature", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SamePathSignature_DifferentParameterNames_GeneratePipelineSucceeds()
    {
        const string json = """
        {
          "openapi": "3.0.3",
          "info": { "title": "Forms", "version": "1" },
          "servers": [{ "url": "https://api.example.com" }],
          "paths": {
            "/v1/forms/{id}": {
              "get": { "operationId": "getForm", "responses": { "200": { "description": "ok" } } }
            },
            "/v1/forms/{formId}": {
              "put": { "operationId": "updateForm", "responses": { "200": { "description": "ok" } } }
            }
          }
        }
        """;
        var specPath = Path.Combine(_workDir, "forms.json");
        await File.WriteAllTextAsync(specPath, json);
        var outDir = Path.Combine(_workDir, "out");

        var exitCode = await GenerateCommand.RunAsync(new GenerateOptions(
            SpecSource: specPath,
            OutputDirectory: outDir,
            Name: "forms",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null), CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "SKILL.md")));
    }
}
