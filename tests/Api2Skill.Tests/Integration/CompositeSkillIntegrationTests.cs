using System.Text.Json;
using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Integration;

/// <summary>T015: two fixtures → one skill; conflicting base URLs → exit 2, no output.</summary>
public class CompositeSkillIntegrationTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-composite-int-" + Guid.NewGuid().ToString("N"));

    public CompositeSkillIntegrationTests() => Directory.CreateDirectory(_workDir);

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
    public async Task TwoFixtures_SameBaseUrl_OneSkillWithBothOps()
    {
        var outDir = Path.Combine(_workDir, "combined");
        var exit = await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("public.json"),
                OutputDirectory: outDir,
                Name: "combined",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: "https://api.example.com",
                SpecParts:
                [
                    new GenerateSpecPart(Fixture("public.json"), "public"),
                    new GenerateSpecPart(Fixture("admin.json"), "admin"),
                ]),
            CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(Path.Combine(outDir, "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "scripts", "call.cs")));

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outDir, SkillManifestIo.FileName)));
        Assert.Equal(2, doc.RootElement.GetProperty("specSources").GetArrayLength());

        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.Contains("## API parts", skillMd, StringComparison.Ordinal);
        Assert.Contains("listCatalogItems", skillMd, StringComparison.Ordinal);
        Assert.Contains("listAuditEvents", skillMd, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConflictingBaseUrls_NoPartialDirectory()
    {
        var outDir = Path.Combine(_workDir, "nope");
        var other = Path.Combine(_workDir, "billing-elsewhere.json");
        var billing = await File.ReadAllTextAsync(Fixture("billing.json"));
        await File.WriteAllTextAsync(
            other,
            billing.Replace("https://api.example.com", "https://billing.elsewhere.com", StringComparison.Ordinal));

        var exit = await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("public.json"),
                OutputDirectory: outDir,
                Name: "nope",
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
                    new GenerateSpecPart(other, "billing"),
                ]),
            CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exit);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task SinglePart_HidesApiPartsSection()
    {
        var outDir = Path.Combine(_workDir, "single");
        var exit = await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("public.json"),
                OutputDirectory: outDir,
                Name: "single",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: null),
            CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exit);
        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.DoesNotContain("## API parts", skillMd, StringComparison.Ordinal);
    }
}
