using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Cli;

/// <summary>T021: merge requires ≥2 skills; refuse existing target without --force.</summary>
public class MergeCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-merge-cmd-" + Guid.NewGuid().ToString("N"));

    public MergeCommandTests() => Directory.CreateDirectory(_workDir);

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

    private async Task<string> GenerateSingleAsync(string fixture, string name)
    {
        var outDir = Path.Combine(_workDir, name);
        var exit = await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture(fixture),
                OutputDirectory: outDir,
                Name: name,
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: "https://api.example.com"),
            CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exit);
        return outDir;
    }

    [Fact]
    public async Task Merge_RequiresTwoSkills_SteersToAddSpec()
    {
        var one = await GenerateSingleAsync("public.json", "only-public");
        var into = Path.Combine(_workDir, "merged");

        var exit = await MergeCommand.RunAsync(into, [one], force: false, onConflict: "fail", CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exit);
        Assert.False(Directory.Exists(into));
    }

    [Fact]
    public async Task Merge_ExistingTargetWithoutForce_Refuses()
    {
        var a = await GenerateSingleAsync("public.json", "pub");
        var b = await GenerateSingleAsync("admin.json", "adm");
        var into = Path.Combine(_workDir, "already");
        Assert.Equal(
            ExitCodes.Success,
            await GenerateCommand.RunAsync(
                new GenerateOptions(
                    SpecSource: Fixture("public.json"),
                    OutputDirectory: into,
                    Name: "already",
                    ScriptKind: "cs",
                    Include: [],
                    Exclude: [],
                    Force: false,
                    Insecure: false,
                    Format: null,
                    BaseUrl: "https://api.example.com"),
                CancellationToken.None));

        var exit = await MergeCommand.RunAsync(into, [a, b], force: false, onConflict: "fail", CancellationToken.None);

        Assert.Equal(ExitCodes.OutputExists, exit);
        var manifest = SkillManifestIo.TryLoad(into);
        Assert.NotNull(manifest);
        Assert.Single(manifest.GetSpecSources());
    }

    [Fact]
    public async Task Merge_TwoSkills_WritesComposite_SourcesUntouched()
    {
        var a = await GenerateSingleAsync("public.json", "pub");
        var b = await GenerateSingleAsync("admin.json", "adm");
        var into = Path.Combine(_workDir, "merged");

        var exit = await MergeCommand.RunAsync(into, [a, b], force: false, onConflict: "fail", CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exit);
        var manifest = SkillManifestIo.TryLoad(into);
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.GetSpecSources().Count);
        Assert.True(Directory.Exists(a));
        Assert.True(Directory.Exists(b));
        Assert.True(File.Exists(Path.Combine(a, SkillManifestIo.FileName)));
        Assert.True(File.Exists(Path.Combine(b, SkillManifestIo.FileName)));
    }
}
