using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Output;

/// <summary>
/// T003 (specs/004-skill-rename-move-on-update): <see cref="SkillWriter.Write"/>'s
/// <c>preserveFromDirectory</c> parameter lets credential/cache files be read from a directory
/// other than the write target — the cross-directory case <c>update --out</c> needs when
/// relocating a skill to a target directory that doesn't exist yet.
/// </summary>
public class SkillWriterTests : IDisposable
{
    private const string SecretsSentinel = "REAL_SECRET_DO_NOT_LEAK";
    private const string AuthConfigSentinel = """{"profiles":[{"name":"default","type":"bearer"}]}""";
    private const string TokenCacheSentinel = """{"aad":{"access_token":"LIVE-TOKEN"}}""";
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-writer-" + Guid.NewGuid().ToString("N"));

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
    public async Task Write_WithPreserveFromDirectory_CopiesCredentialAndCacheFilesFromSourceIntoNewTarget()
    {
        var model = await BuildMultiAuthModelAsync();
        var sourceDir = Path.Combine(_workDir, "old");
        var targetDir = Path.Combine(_workDir, "new");

        SkillWriter.Write(model, sourceDir, force: false, new CsFileEmitter());
        await File.WriteAllTextAsync(
            Path.Combine(sourceDir, SecretsScaffold.RealSecretsFileName),
            $$"""{ "apiKeyAuth": { "apiKey": "{{SecretsSentinel}}" } }""");
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "auth.json"), AuthConfigSentinel);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, SecretsScaffold.TokenCacheFileName), TokenCacheSentinel);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, SecretsScaffold.TokenCacheFileName + ".lock"), "lock-bytes");

        Assert.False(Directory.Exists(targetDir));

        SkillWriter.Write(model, targetDir, force: true, new CsFileEmitter(), preserveFromDirectory: sourceDir);

        Assert.Equal(
            $$"""{ "apiKeyAuth": { "apiKey": "{{SecretsSentinel}}" } }""",
            await File.ReadAllTextAsync(Path.Combine(targetDir, SecretsScaffold.RealSecretsFileName)));
        Assert.Equal(AuthConfigSentinel, await File.ReadAllTextAsync(Path.Combine(targetDir, "auth.json")));
        Assert.Equal(TokenCacheSentinel, await File.ReadAllTextAsync(Path.Combine(targetDir, SecretsScaffold.TokenCacheFileName)));
        Assert.Equal("lock-bytes", await File.ReadAllTextAsync(Path.Combine(targetDir, SecretsScaffold.TokenCacheFileName + ".lock")));

        // Source directory itself is untouched by SkillWriter — deleting it is UpdateCommand's job.
        Assert.True(File.Exists(Path.Combine(sourceDir, SecretsScaffold.RealSecretsFileName)));
    }

    [Fact]
    public async Task Write_WithPreserveFromDirectory_WhenSourceHasNoCredentialFiles_WritesNoneAtTarget()
    {
        var model = await BuildMultiAuthModelAsync();
        var sourceDir = Path.Combine(_workDir, "old-empty");
        var targetDir = Path.Combine(_workDir, "new-empty");
        Directory.CreateDirectory(sourceDir);

        SkillWriter.Write(model, targetDir, force: true, new CsFileEmitter(), preserveFromDirectory: sourceDir);

        Assert.False(File.Exists(Path.Combine(targetDir, SecretsScaffold.RealSecretsFileName)));
        Assert.False(File.Exists(Path.Combine(targetDir, "auth.json")));
        Assert.False(File.Exists(Path.Combine(targetDir, SecretsScaffold.TokenCacheFileName)));
    }
}
