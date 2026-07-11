using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Output;

/// <summary>T021: --force regenerates generated files but never touches a real secrets.json (FR-009, NFR-1, AC-5/SC-005).</summary>
public class ForcePreservesSecretsTests : IDisposable
{
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
}
