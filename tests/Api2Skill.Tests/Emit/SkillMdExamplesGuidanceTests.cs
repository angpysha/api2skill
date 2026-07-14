using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

public class SkillMdExamplesGuidanceTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-ex-skillmd-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SkillMd_ContainsPreferExamplesAndFailureProtocol()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));

        var outDir = Path.Combine(_workDir, "out");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.Contains("## Authored examples", skillMd, StringComparison.Ordinal);
        Assert.Contains("Prefer authored examples", skillMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do **not** invent a JSON body", skillMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failure protocol", skillMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicitly approves", skillMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ask the user", skillMd, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api2skill example sync", skillMd, StringComparison.Ordinal);
    }
}
