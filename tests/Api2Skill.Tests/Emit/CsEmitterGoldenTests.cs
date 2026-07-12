using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

/// <summary>
/// Golden/snapshot test (T013): generate a skill from the petstore fixture and diff the whole
/// output tree against the approved copy in fixtures/__approved__/petstore-cs. Guards both
/// output correctness and determinism (NFR-4) — a regenerate-twice determinism check lives
/// alongside it here since it's cheap to add and covers the same code path.
/// </summary>
public class CsEmitterGoldenTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-tests-" + Guid.NewGuid().ToString("N"));

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static string ApprovedDir => FixturePath(Path.Combine("__approved__", "petstore-cs"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static async Task<SkillModel> BuildPetstoreModelAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        return SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));
    }

    private static string PetstoreManifestJson(string scriptKind) =>
        SkillManifestIo.Serialize(new SkillManifest(
            Name: "petstore",
            SpecSource: "tests/Api2Skill.Tests/fixtures/petstore.json",
            ScriptKind: scriptKind,
            Include: [],
            Exclude: [],
            Format: null,
            BaseUrl: null,
            Insecure: false));

    [Fact]
    public async Task Generate_MatchesApprovedGoldenTree()
    {
        var model = await BuildPetstoreModelAsync();
        var outputDir = Path.Combine(_workDir, "out");

        SkillWriter.Write(model, outputDir, force: false, new CsFileEmitter(), manifestJson: PetstoreManifestJson("cs"));

        var approvedFiles = Directory.GetFiles(ApprovedDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(ApprovedDir, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        var actualFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(outputDir, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(approvedFiles, actualFiles);

        foreach (var relative in approvedFiles)
        {
            var expected = File.ReadAllText(Path.Combine(ApprovedDir, relative));
            var actual = File.ReadAllText(Path.Combine(outputDir, relative));
            Assert.True(expected == actual, $"Mismatch in {relative}:\n--- expected ---\n{expected}\n--- actual ---\n{actual}");
        }
    }

    [Fact]
    public async Task Generate_IsByteStable_AcrossTwoRuns()
    {
        var model = await BuildPetstoreModelAsync();
        var firstDir = Path.Combine(_workDir, "first");
        var secondDir = Path.Combine(_workDir, "second");

        SkillWriter.Write(model, firstDir, force: false, new CsFileEmitter(), manifestJson: PetstoreManifestJson("cs"));
        SkillWriter.Write(model, secondDir, force: false, new CsFileEmitter(), manifestJson: PetstoreManifestJson("cs"));

        var relativeFiles = Directory.GetFiles(firstDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(firstDir, p))
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (var relative in relativeFiles)
        {
            var first = File.ReadAllText(Path.Combine(firstDir, relative));
            var second = File.ReadAllText(Path.Combine(secondDir, relative));
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public async Task GeneratedDispatcher_CompactSkillMdStaysIndexOnly_NoParameterDetail()
    {
        // SC-003 / FR-004: SKILL.md must not carry per-parameter detail — that lives only in
        // reference/<tag>.md (progressive disclosure).
        var model = await BuildPetstoreModelAsync();
        var outputDir = Path.Combine(_workDir, "compact");
        SkillWriter.Write(model, outputDir, force: false, new CsFileEmitter(), manifestJson: PetstoreManifestJson("cs"));

        var skillMd = File.ReadAllText(Path.Combine(outputDir, "SKILL.md"));
        Assert.DoesNotContain("ID of pet to return", skillMd); // parameter description — reference-only
        Assert.Contains("reference/pet.md", skillMd);

        var referenceMd = File.ReadAllText(Path.Combine(outputDir, "reference", "pet.md"));
        Assert.Contains("ID of pet to return", referenceMd);
    }
}
