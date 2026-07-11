using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

/// <summary>
/// T031: golden/snapshot tests for the .fsx and .csx emitters, mirroring
/// CsEmitterGoldenTests — proves both emitters produce their expected output and stay
/// byte-stable (NFR-4). Whether the *generated* scripts actually run is covered by
/// Integration/EmitterRunnerTests (real dotnet fsi / dotnet script execution) and by manual
/// verification recorded in the WI-US4 checkpoint.
/// </summary>
public class FsxCsxGoldenTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-fsxcsx-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

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

    private static void AssertMatchesGolden(string outputDir, string approvedDir)
    {
        var approvedFiles = Directory.GetFiles(approvedDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(approvedDir, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        var actualFiles = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(outputDir, p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(approvedFiles, actualFiles);
        foreach (var relative in approvedFiles)
        {
            var expected = File.ReadAllText(Path.Combine(approvedDir, relative));
            var actual = File.ReadAllText(Path.Combine(outputDir, relative));
            Assert.True(expected == actual, $"Mismatch in {relative}");
        }
    }

    [Fact]
    public async Task FsxEmitter_MatchesApprovedGoldenTree()
    {
        var model = await BuildPetstoreModelAsync();
        var outputDir = Path.Combine(_workDir, "fsx");
        SkillWriter.Write(model, outputDir, force: false, new FsxEmitter(), manifestJson: PetstoreManifestJson("fsx"));

        AssertMatchesGolden(outputDir, FixturePath(Path.Combine("__approved__", "petstore-fsx")));
    }

    [Fact]
    public async Task CsxEmitter_MatchesApprovedGoldenTree()
    {
        var model = await BuildPetstoreModelAsync();
        var outputDir = Path.Combine(_workDir, "csx");
        SkillWriter.Write(model, outputDir, force: false, new CsxEmitter(), manifestJson: PetstoreManifestJson("csx"));

        AssertMatchesGolden(outputDir, FixturePath(Path.Combine("__approved__", "petstore-csx")));
    }

    [Theory]
    [InlineData("fsx", "call.fsx")]
    [InlineData("csx", "call.csx")]
    public async Task Emitter_BakesInsecureDefault_WhenModelRequestsIt(string emitterKey, string scriptFile)
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion,
            new BuildOptions(Name: "petstore", InsecureDefault: true));

        IScriptEmitter emitter = emitterKey == "fsx" ? new FsxEmitter() : new CsxEmitter();
        var outputDir = Path.Combine(_workDir, "insecure-" + emitterKey);
        SkillWriter.Write(model, outputDir, force: false, emitter);

        var source = await File.ReadAllTextAsync(Path.Combine(outputDir, "scripts", scriptFile));
        Assert.Contains(emitterKey == "fsx" ? "\"1\"" : "\"1\";", source);
    }

    [Fact]
    public void CsxEmitter_EnablesNullableContext()
    {
        // Regression guard: dotnet-script does NOT default to nullable-enabled, unlike a
        // modern .csproj — the `string?` annotations in the generated script fail to compile
        // (CS8632) without an explicit `#nullable enable` at the top.
        var model = new SkillModel("x", "X", null, null, null, SpecVersionKind.OpenApi3_0, [], [], []);
        var dir = Directory.CreateDirectory(Path.Combine(_workDir, "nullable-check"));
        new CsxEmitter().Emit(model, dir);

        var source = File.ReadAllText(Path.Combine(dir.FullName, "scripts", "call.csx"));
        Assert.StartsWith("// Generated by api2skill", source);
        Assert.Contains("#nullable enable", source);
    }

    [Fact]
    public void FsxEmitter_OpensSystemThreadingTasks()
    {
        // Regression guard: Task<'t> isn't in scope without this open, and the error
        // (FS0039, "type 'Task' is not defined") only surfaces when the *generated* .fsx is
        // actually compiled by dotnet fsi — this was caught that way, not by this emitter's
        // own (C#) build.
        var model = new SkillModel("x", "X", null, null, null, SpecVersionKind.OpenApi3_0, [], [], []);
        var dir = Directory.CreateDirectory(Path.Combine(_workDir, "task-open-check"));
        new FsxEmitter().Emit(model, dir);

        var source = File.ReadAllText(Path.Combine(dir.FullName, "scripts", "call.fsx"));
        Assert.Contains("open System.Threading.Tasks", source);
    }
}
