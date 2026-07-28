using System.Text.Json;
using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Cli;

/// <summary>T014: multi <c>--spec</c> / <c>--spec-id</c> / <c>--on-conflict</c> for generate.</summary>
public class MultiSpecGenerateTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-multi-gen-" + Guid.NewGuid().ToString("N"));

    public MultiSpecGenerateTests() => Directory.CreateDirectory(_workDir);

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
    public void BuildSpecParts_WithRepeatableSpec_IgnoresPositional()
    {
        var parts = GenerateCommand.BuildSpecParts(
            positional: "./ignored.json",
            specs: ["./a.json", "./b.json"],
            ids: ["public", "admin"],
            labels: ["Public", "Admin"]);

        Assert.Equal(2, parts.Count);
        Assert.Equal("./a.json", parts[0].Source);
        Assert.Equal("public", parts[0].Id);
        Assert.Equal("Public", parts[0].Label);
        Assert.Equal("admin", parts[1].Id);
    }

    [Fact]
    public void BuildSpecParts_PositionalOnly_SinglePart()
    {
        var parts = GenerateCommand.BuildSpecParts("./solo.json", [], [], []);
        Assert.Single(parts);
        Assert.Equal("./solo.json", parts[0].Source);
        Assert.Null(parts[0].Id);
    }

    [Fact]
    public async Task Generate_TwoSpecs_WritesCompositeManifestAndApiParts()
    {
        var outDir = Path.Combine(_workDir, "acme");
        var options = new GenerateOptions(
            SpecSource: Fixture("public.json"),
            OutputDirectory: outDir,
            Name: "acme",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null,
            SpecParts:
            [
                new GenerateSpecPart(Fixture("public.json"), "public", "Public"),
                new GenerateSpecPart(Fixture("admin.json"), "admin", "Admin"),
            ]);

        var exit = await GenerateCommand.RunAsync(options, CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exit);

        var manifest = SkillManifestIo.TryLoad(outDir);
        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.GetSpecSources().Count);
        Assert.Equal("public", manifest.GetSpecSources()[0].Id);
        Assert.Equal("admin", manifest.GetSpecSources()[1].Id);

        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.Contains("## API parts", skillMd, StringComparison.Ordinal);
        Assert.Contains("getMyProfile", skillMd, StringComparison.Ordinal);
        Assert.Contains("listUsers", skillMd, StringComparison.Ordinal);
        Assert.Contains("[public] users", skillMd, StringComparison.Ordinal);
        Assert.Contains("[admin] users", skillMd, StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outDir, SkillManifestIo.FileName)));
        Assert.Equal(2, doc.RootElement.GetProperty("specSources").GetArrayLength());
    }

    [Fact]
    public async Task Generate_ConflictingBaseUrls_ExitsUsage_NoOutput()
    {
        var outDir = Path.Combine(_workDir, "should-fail");
        var mismatched = Path.Combine(_workDir, "other-host.json");
        var admin = await File.ReadAllTextAsync(Fixture("admin.json"));
        await File.WriteAllTextAsync(
            mismatched,
            admin.Replace("https://api.example.com", "https://other.example.com", StringComparison.Ordinal));

        var options = new GenerateOptions(
            SpecSource: Fixture("public.json"),
            OutputDirectory: outDir,
            Name: "bad",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null,
            SpecParts:
            [
                new GenerateSpecPart(Fixture("public.json"), "public"),
                new GenerateSpecPart(mismatched, "admin"),
            ]);

        var exit = await GenerateCommand.RunAsync(options, CancellationToken.None);
        Assert.Equal(ExitCodes.UsageError, exit);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task Generate_DuplicateSpecId_ExitsUsage()
    {
        var outDir = Path.Combine(_workDir, "dup");
        var options = new GenerateOptions(
            SpecSource: Fixture("public.json"),
            OutputDirectory: outDir,
            Name: "dup",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null,
            SpecParts:
            [
                new GenerateSpecPart(Fixture("public.json"), "same"),
                new GenerateSpecPart(Fixture("admin.json"), "same"),
            ]);

        var exit = await GenerateCommand.RunAsync(options, CancellationToken.None);
        Assert.Equal(ExitCodes.UsageError, exit);
    }
}
