using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

/// <summary>T014: SKILL.md **Auth profile names** section when scaffold guidance is present.</summary>
public class SkillMdWriterScaffoldTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-skillmd-scaffold-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Write_EmitsAuthProfileNamesSection_WhenScaffoldGuidancePresent()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        var baseModel = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));
        var scaffold = AuthScaffold.Build(baseModel);
        var model = baseModel with { AuthScaffoldGuidance = scaffold.Guidance };

        var outDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), scaffoldAuthJson: scaffold.Json);

        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.Contains("## Auth profile names", skillMd, StringComparison.Ordinal);
        Assert.Contains("`bearerAuth`", skillMd, StringComparison.Ordinal);
        Assert.Contains("manual only", skillMd, StringComparison.Ordinal);
        Assert.Contains("--auth-config ./auth.json --force", skillMd, StringComparison.Ordinal);
    }
}
