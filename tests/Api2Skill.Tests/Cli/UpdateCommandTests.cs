using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Cli;

/// <summary>
/// T011/T015/T016 (specs/003-skill-update-command): <see cref="UpdateCommand.RunAsync"/> exit
/// codes and manifest rewriting, calling it directly (internal, exposed via
/// InternalsVisibleTo) matching <see cref="ExitCodeTests"/>'s existing pattern.
/// </summary>
public class UpdateCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-update-cmd-" + Guid.NewGuid().ToString("N"));

    public UpdateCommandTests() => Directory.CreateDirectory(_workDir);

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
    public async Task NoManifest_ExitsTwo_WritesNothing()
    {
        var target = Path.Combine(_workDir, "not-a-skill");
        Directory.CreateDirectory(target);

        var exitCode = await UpdateCommand.RunAsync(target, null, CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Empty(Directory.GetFiles(target));
    }

    [Fact]
    public async Task MalformedManifest_ExitsTwo()
    {
        var target = Path.Combine(_workDir, "corrupt-skill");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, SkillManifestIo.FileName), "{ not valid json");

        var exitCode = await UpdateCommand.RunAsync(target, null, CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task ManifestTargetDoesNotExistAtAll_ExitsTwo()
    {
        var target = Path.Combine(_workDir, "does-not-exist");

        var exitCode = await UpdateCommand.RunAsync(target, null, CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task SuccessfulUpdate_WithNewSpecSource_RewritesManifestToRecordIt()
    {
        var skillDir = Path.Combine(_workDir, "skill");
        var generateOptions = new GenerateOptions(
            SpecSource: FixturePath("petstore.json"),
            OutputDirectory: skillDir,
            Name: "petstore",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null);
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(generateOptions, CancellationToken.None));

        var newSpecPath = Path.Combine(_workDir, "petstore-v2.json");
        File.Copy(FixturePath("petstore.json"), newSpecPath);

        var exitCode = await UpdateCommand.RunAsync(skillDir, newSpecPath, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        var manifest = SkillManifestIo.TryLoad(skillDir);
        Assert.NotNull(manifest);
        Assert.Equal(newSpecPath, manifest.SpecSource);
    }
}
