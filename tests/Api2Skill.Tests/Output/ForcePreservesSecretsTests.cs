using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Output;

/// <summary>
/// T021: --force regenerates generated files but never touches a real secrets.json (FR-009,
/// NFR-1, AC-5/SC-005). Also T039: the no-partial-output guarantee — SkillWriter stages into a
/// sibling temp directory and only replaces the target once every writer has succeeded, so a
/// failure partway through can neither leave a half-written skill nor (on --force) destroy the
/// old one, including a real secrets.json only held in memory until the very end.
/// </summary>
public class ForcePreservesSecretsTests : IDisposable
{
    /// <summary>A fake emitter that always throws, for exercising SkillWriter's failure path.</summary>
    private sealed class ThrowingEmitter : IScriptEmitter
    {
        public string Key => "throwing";
        public string FileExtension => "throwing";
        public string RunnerDescription => "n/a";
        public void Emit(SkillModel model, DirectoryInfo skillDirectory) =>
            throw new InvalidOperationException("simulated emitter failure");
    }

    private const string Sentinel = "REAL_SECRET_DO_NOT_LEAK";
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-force-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<SkillModel> BuildMultiAuthModelAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        return SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));
    }

    [Fact]
    public async Task Write_WithoutForce_FailsWhenDirectoryExists()
    {
        var model = await BuildMultiAuthModelAsync();
        var outDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        Assert.Throws<SkillDirectoryExistsException>(() =>
            SkillWriter.Write(model, outDir, force: false, new CsFileEmitter()));
    }

    [Fact]
    public async Task Write_WithForce_PreservesRealSecretsFileByteForByte()
    {
        var model = await BuildMultiAuthModelAsync();
        var outDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        var secretsPath = Path.Combine(outDir, SecretsScaffold.RealSecretsFileName);
        var realSecrets = $$"""{ "apiKeyAuth": { "apiKey": "{{Sentinel}}" } }""";
        await File.WriteAllTextAsync(secretsPath, realSecrets);

        SkillWriter.Write(model, outDir, force: true, new CsFileEmitter());

        Assert.Equal(realSecrets, await File.ReadAllTextAsync(secretsPath));
    }

    [Fact]
    public async Task Write_WithForce_NeverLeaksRealSecretIntoAnyGeneratedFile()
    {
        var model = await BuildMultiAuthModelAsync();
        var outDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        await File.WriteAllTextAsync(
            Path.Combine(outDir, SecretsScaffold.RealSecretsFileName),
            $$"""{ "apiKeyAuth": { "apiKey": "{{Sentinel}}" } }""");

        SkillWriter.Write(model, outDir, force: true, new CsFileEmitter());

        var generatedFiles = Directory.GetFiles(outDir, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f) != SecretsScaffold.RealSecretsFileName);

        foreach (var file in generatedFiles)
        {
            Assert.DoesNotContain(Sentinel, await File.ReadAllTextAsync(file));
        }
    }

    [Fact]
    public async Task Write_WhenEmitterThrows_LeavesNoTargetDirectoryBehind()
    {
        var model = await BuildMultiAuthModelAsync();
        var outDir = Path.Combine(_workDir, "out-fresh-fail");

        Assert.Throws<InvalidOperationException>(() =>
            SkillWriter.Write(model, outDir, force: false, new ThrowingEmitter()));

        Assert.False(Directory.Exists(outDir), "A failed generation must not leave a partial target directory (FR-010/T039).");
        AssertNoLeftoverStagingDirectories(outDir);
    }

    [Fact]
    public async Task Write_WhenEmitterThrowsDuringForceRegenerate_LeavesTheOldSkillAndItsSecretsUntouched()
    {
        var model = await BuildMultiAuthModelAsync();
        var outDir = Path.Combine(_workDir, "out-force-fail");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        var secretsPath = Path.Combine(outDir, SecretsScaffold.RealSecretsFileName);
        var realSecrets = $$"""{ "apiKeyAuth": { "apiKey": "{{Sentinel}}" } }""";
        await File.WriteAllTextAsync(secretsPath, realSecrets);
        var skillMdBefore = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));

        Assert.Throws<InvalidOperationException>(() =>
            SkillWriter.Write(model, outDir, force: true, new ThrowingEmitter()));

        // The old skill — including the real secrets file — must survive a failed
        // regeneration untouched: staging happens before anything about the target is
        // deleted (T039).
        Assert.Equal(realSecrets, await File.ReadAllTextAsync(secretsPath));
        Assert.Equal(skillMdBefore, await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md")));
        AssertNoLeftoverStagingDirectories(outDir);
    }

    private static void AssertNoLeftoverStagingDirectories(string targetDir)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(targetDir))!;
        var name = Path.GetFileName(targetDir);
        if (!Directory.Exists(parent))
        {
            return;
        }
        var leftovers = Directory.GetDirectories(parent, $".{name}.api2skill-staging-*");
        Assert.Empty(leftovers);
    }
}
