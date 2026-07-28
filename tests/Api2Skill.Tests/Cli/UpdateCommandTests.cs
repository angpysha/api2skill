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

    /// <summary>T008/US1 (specs/004-skill-rename-move-on-update): --name alone regenerates in place under the new name.</summary>
    [Fact]
    public async Task Update_WithNameOnly_RegeneratesInPlaceAndRewritesManifestName()
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

        var exitCode = await UpdateCommand.RunAsync(skillDir, null, CancellationToken.None, newName: "petstore-v2");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(Directory.Exists(skillDir)); // in place — no move
        var manifest = SkillManifestIo.TryLoad(skillDir);
        Assert.NotNull(manifest);
        Assert.Equal("petstore-v2", manifest.Name);
    }

    /// <summary>T010/US2: --out pointing at an existing non-empty foreign directory fails clearly and touches nothing.</summary>
    [Fact]
    public async Task Update_WithOutCollidingWithNonEmptyForeignDirectory_ExitsTwo_WritesNothingEitherSide()
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

        var foreignDir = Path.Combine(_workDir, "foreign");
        Directory.CreateDirectory(foreignDir);
        await File.WriteAllTextAsync(Path.Combine(foreignDir, "unrelated.txt"), "not an api2skill skill");

        var exitCode = await UpdateCommand.RunAsync(skillDir, null, CancellationToken.None, newOutputDirectory: foreignDir);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.True(Directory.Exists(skillDir)); // source untouched
        Assert.True(File.Exists(Path.Combine(foreignDir, "unrelated.txt"))); // destination untouched
        Assert.Single(Directory.GetFileSystemEntries(foreignDir)); // nothing new written into it
    }

    private static string MultiFixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "multi-spec", name);

    [Fact]
    public async Task MultiPart_PositionalWithoutSpecId_UsageError()
    {
        var skillDir = Path.Combine(_workDir, "multi");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: MultiFixture("public.json"),
                OutputDirectory: skillDir,
                Name: "multi",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: "https://api.example.com",
                SpecParts:
                [
                    new GenerateSpecPart(MultiFixture("public.json"), "public"),
                    new GenerateSpecPart(MultiFixture("admin.json"), "admin"),
                ]),
            CancellationToken.None));

        var exit = await UpdateCommand.RunAsync(skillDir, MultiFixture("admin.json"), CancellationToken.None);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task MultiPart_WithSpecId_ReplacesOnePart()
    {
        var skillDir = Path.Combine(_workDir, "multi2");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: MultiFixture("public.json"),
                OutputDirectory: skillDir,
                Name: "multi2",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: "https://api.example.com",
                SpecParts:
                [
                    new GenerateSpecPart(MultiFixture("public.json"), "public"),
                    new GenerateSpecPart(MultiFixture("admin.json"), "admin"),
                ]),
            CancellationToken.None));

        var newAdmin = Path.Combine(_workDir, "admin-new.json");
        File.Copy(MultiFixture("admin.json"), newAdmin);
        var exit = await UpdateCommand.RunAsync(skillDir, newAdmin, CancellationToken.None, specId: "admin");
        Assert.Equal(ExitCodes.Success, exit);
        var parts = SkillManifestIo.TryLoad(skillDir)!.GetSpecSources();
        Assert.Equal(newAdmin, parts.Single(p => p.Id == "admin").Source);
        Assert.Contains(parts, p => p.Id == "public");
    }
}
