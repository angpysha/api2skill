using Api2Skill.Cli;
using Api2Skill.Emit;
using Api2Skill.Examples;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Output;

public class ForcePreservesExamplesTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-ex-force-" + Guid.NewGuid().ToString("N"));

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
    public async Task Write_WithForce_PreservesExamplesAndRelinks()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));

        var outDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        ExampleStore.Write(outDir, "addPet", "happy", """{"name":"doggie"}""", null, force: false);
        ExampleReferenceLinker.SyncSkill(outDir, ExampleReferenceLinker.KnownOperationIds(outDir));

        var requestPath = Path.Combine(outDir, "examples", "addPet", "happy", "request.json");
        var payload = await File.ReadAllTextAsync(requestPath);

        SkillWriter.Write(model, outDir, force: true, new CsFileEmitter());

        Assert.True(File.Exists(requestPath));
        Assert.Equal(payload, await File.ReadAllTextAsync(requestPath));
        var petMd = await File.ReadAllTextAsync(Path.Combine(outDir, "reference", "pet.md"));
        Assert.Contains("examples/addPet/happy/request.json", petMd, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(outDir, "reference", "schemas", "Pet.json")));
        Assert.True(File.Exists(Path.Combine(outDir, "reference", "schemas", "PetInput.json")));
    }

    [Fact]
    public async Task Generate_Force_PreservesExamples_ViaCli()
    {
        var outDir = Path.Combine(_workDir, "cli");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: FixturePath("petstore.json"),
                OutputDirectory: outDir,
                Name: null,
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: null,
                AuthConfigPath: null,
                AuthShorthand: null,
                Login: false),
            CancellationToken.None));

        var req = Path.Combine(_workDir, "body.json");
        await File.WriteAllTextAsync(req, """{"name":"doggie"}""");
        using var sw = new StringWriter();
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunAdd(outDir, "addPet", "happy", req, null, false, sw, sw));

        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: FixturePath("petstore.json"),
                OutputDirectory: outDir,
                Name: null,
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: true,
                Insecure: false,
                Format: null,
                BaseUrl: null,
                AuthConfigPath: null,
                AuthShorthand: null,
                Login: false),
            CancellationToken.None));

        Assert.True(File.Exists(Path.Combine(outDir, "examples", "addPet", "happy", "request.json")));
        Assert.Contains(
            "examples/addPet/happy/request.json",
            await File.ReadAllTextAsync(Path.Combine(outDir, "reference", "pet.md")),
            StringComparison.Ordinal);
    }
}
