using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Cli;

/// <summary>T026: add-spec duplicate spec-id fails; default id from filename.</summary>
public class AddSpecCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-addspec-cmd-" + Guid.NewGuid().ToString("N"));

    public AddSpecCommandTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "multi-spec", name);

    [Fact]
    public async Task AddSpec_DefaultIdFromFilename()
    {
        var skill = Path.Combine(_workDir, "skill");
        Assert.Equal(
            ExitCodes.Success,
            await GenerateCommand.RunAsync(
                new GenerateOptions(
                    SpecSource: Fixture("public.json"),
                    OutputDirectory: skill,
                    Name: "skill",
                    ScriptKind: "cs",
                    Include: [],
                    Exclude: [],
                    Force: false,
                    Insecure: false,
                    Format: null,
                    BaseUrl: "https://api.example.com",
                    SpecParts:
                    [
                        new GenerateSpecPart(Fixture("public.json"), "public"),
                        new GenerateSpecPart(Fixture("admin.json"), "admin"),
                    ]),
                CancellationToken.None));

        var exit = await AddSpecCommand.RunAsync(
            skill, Fixture("billing.json"), specId: null, specLabel: null, onConflict: "fail", CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exit);
        var manifest = SkillManifestIo.TryLoad(skill);
        Assert.NotNull(manifest);
        Assert.Equal(3, manifest.GetSpecSources().Count);
        Assert.Contains(manifest.GetSpecSources(), p => p.Id == "billing");
    }

    [Fact]
    public async Task AddSpec_DuplicateSpecId_Fails()
    {
        var skill = Path.Combine(_workDir, "skill");
        Assert.Equal(
            ExitCodes.Success,
            await GenerateCommand.RunAsync(
                new GenerateOptions(
                    SpecSource: Fixture("public.json"),
                    OutputDirectory: skill,
                    Name: "skill",
                    ScriptKind: "cs",
                    Include: [],
                    Exclude: [],
                    Force: false,
                    Insecure: false,
                    Format: null,
                    BaseUrl: "https://api.example.com",
                    SpecParts:
                    [
                        new GenerateSpecPart(Fixture("public.json"), "public"),
                    ]),
                CancellationToken.None));

        var exit = await AddSpecCommand.RunAsync(
            skill, Fixture("admin.json"), specId: "public", specLabel: null, onConflict: "fail", CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exit);
        Assert.Single(SkillManifestIo.TryLoad(skill)!.GetSpecSources());
    }
}
