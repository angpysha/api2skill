using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

public class SchemaWriterTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-schema-writer-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public async Task Generate_WritesPetAndPetInputRawSchemaFiles()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));

        var outputDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outputDir, force: false, new CsFileEmitter());

        var petPath = Path.Combine(outputDir, "reference", "schemas", "Pet.json");
        var petInputPath = Path.Combine(outputDir, "reference", "schemas", "PetInput.json");
        Assert.True(File.Exists(petPath));
        Assert.True(File.Exists(petInputPath));

        var petJson = await File.ReadAllTextAsync(petPath);
        Assert.Contains("\"properties\"", petJson);
        Assert.Contains("#/components/schemas/Category", petJson);
        Assert.Contains("#/components/schemas/Tag", petJson);

        var petInputJson = await File.ReadAllTextAsync(petInputPath);
        Assert.Contains("\"properties\"", petInputJson);
        Assert.Contains("#/components/schemas/Category", petInputJson);
    }

    [Fact]
    public async Task ForceRegenerate_UpdatesSchemaFileContent()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));

        var outputDir = Path.Combine(_workDir, "force");
        SkillWriter.Write(model, outputDir, force: false, new CsFileEmitter());

        var petPath = Path.Combine(outputDir, "reference", "schemas", "Pet.json");
        var original = await File.ReadAllTextAsync(petPath);

        var mutatedJson = (await File.ReadAllTextAsync(FixturePath("petstore.json")))
            .Replace("\"Pet id\"", "\"UPDATED_MARKER Pet id\"", StringComparison.Ordinal);
        await using var mutatedStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(mutatedJson));
        var mutatedLoaded = await OpenApiLoader.LoadAsync(mutatedStream, "json");
        var mutatedModel = SkillModelBuilder.Build(
            mutatedLoaded.Document, mutatedLoaded.SpecVersion, new BuildOptions(Name: "petstore"));

        SkillWriter.Write(mutatedModel, outputDir, force: true, new CsFileEmitter());

        var updated = await File.ReadAllTextAsync(petPath);
        Assert.NotEqual(original, updated);
        Assert.Contains("UPDATED_MARKER", updated);
    }
}
