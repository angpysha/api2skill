using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests;

/// <summary>
/// T041: for a fixed (spec, options) pair, every emitted file must be byte-identical across
/// runs (NFR-4) — this is what makes a <c>--force</c> diff meaningful and is required for the
/// golden-file tests elsewhere in the suite to be trustworthy in the first place. Covers all
/// three emitters and both fixtures, going a level beyond the single-emitter check already in
/// CsEmitterGoldenTests.Generate_IsByteStable_AcrossTwoRuns.
/// </summary>
public class DeterminismTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-determinism-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<SkillModel> BuildModelAsync(string fixture, string format)
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath(fixture)));
        var loaded = await OpenApiLoader.LoadAsync(stream, format);
        return SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "det-test"));
    }

    public static IEnumerable<object[]> EmitterFixtureCombinations()
    {
        foreach (var (fixture, format) in new[] { ("petstore.json", "json"), ("multi-auth.yaml", "yaml") })
        {
            foreach (IScriptEmitter emitter in new IScriptEmitter[] { new CsFileEmitter(), new FsxEmitter(), new CsxEmitter() })
            {
                yield return [fixture, format, emitter];
            }
        }
    }

    [Theory]
    [MemberData(nameof(EmitterFixtureCombinations))]
    public async Task Write_ProducesByteIdenticalOutput_AcrossTwoIndependentRuns(string fixture, string format, IScriptEmitter emitter)
    {
        var model = await BuildModelAsync(fixture, format);

        var firstDir = Path.Combine(_workDir, $"{emitter.Key}-{Path.GetFileNameWithoutExtension(fixture)}-first");
        var secondDir = Path.Combine(_workDir, $"{emitter.Key}-{Path.GetFileNameWithoutExtension(fixture)}-second");

        // Fresh model instances per write, mirroring two independent `generate` invocations
        // rather than reusing mutable state between them.
        SkillWriter.Write(await BuildModelAsync(fixture, format), firstDir, force: false, emitter);
        SkillWriter.Write(await BuildModelAsync(fixture, format), secondDir, force: false, emitter);

        var relativeFiles = Directory.GetFiles(firstDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(firstDir, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var secondFiles = Directory.GetFiles(secondDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(secondDir, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(relativeFiles, secondFiles);

        foreach (var relative in relativeFiles)
        {
            var firstBytes = await File.ReadAllBytesAsync(Path.Combine(firstDir, relative));
            var secondBytes = await File.ReadAllBytesAsync(Path.Combine(secondDir, relative));
            Assert.True(firstBytes.AsSpan().SequenceEqual(secondBytes), $"Byte mismatch in {relative} for emitter '{emitter.Key}' / fixture '{fixture}'.");
        }
    }

    [Fact]
    public async Task Write_TagAndOperationOrder_IsStableAcrossRuns_NotJustFileContent()
    {
        // A subtler determinism failure mode than byte-identical files: if tag/operation
        // ordering came from unordered collection enumeration (e.g. a plain Dictionary/HashSet
        // without an explicit OrderBy), two runs could coincidentally produce the same bytes in
        // a small test fixture while still being fragile. Assert the order directly, not just
        // the end result.
        var model1 = await BuildModelAsync("petstore.json", "json");
        var model2 = await BuildModelAsync("petstore.json", "json");

        Assert.Equal(model1.Tags.Select(t => t.Tag), model2.Tags.Select(t => t.Tag));
        foreach (var (tag1, tag2) in model1.Tags.Zip(model2.Tags))
        {
            Assert.Equal(tag1.Operations.Select(o => o.OperationId), tag2.Operations.Select(o => o.OperationId));
        }
        Assert.Equal(model1.SecuritySchemes.Select(s => s.Id), model2.SecuritySchemes.Select(s => s.Id));
    }
}
