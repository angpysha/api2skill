using Api2Skill.Emit;
using Api2Skill.Model;

namespace Api2Skill.Tests.Emit;

public class ReferencePathsTests
{
    [Fact]
    public void SanitizeTagFileName_ReplacesSlashAndWhitespace()
    {
        Assert.Equal("Group-Item", ReferencePaths.SanitizeTagFileName("Group / Item"));
    }

    [Fact]
    public void BuildTagFileStems_DisambiguatesCollisions()
    {
        var stems = ReferencePaths.BuildTagFileStems(["A/B", "A-B"]);
        Assert.Equal(2, stems.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ReferenceWriter_WritesSanitizedFile_ForTagWithSlash()
    {
        var model = new SkillModel(
            Name: "demo",
            Title: "Demo",
            Description: null,
            Version: "1.0",
            BaseUrl: "https://api.example.com",
            SpecVersion: SpecVersionKind.OpenApi3_0,
            SecuritySchemes: [],
            Tags:
            [
                new TagGroup(
                    "Group / Item",
                    null,
                    [
                        new OperationModel(
                            "doThing",
                            HttpMethod.Get,
                            "/v1/things",
                            "summary",
                            null,
                            [],
                            null,
                            [],
                            []),
                    ]),
            ],
            Warnings: []);

        var dir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "api2skill-ref-" + Guid.NewGuid().ToString("N")));
        dir.Create();
        try
        {
            ReferenceWriter.Write(model, dir);
            var expected = Path.Combine(dir.FullName, "reference", "Group-Item.md");
            Assert.True(File.Exists(expected), $"Expected reference file at {expected}");
            Assert.DoesNotContain(
                Directory.EnumerateFiles(Path.Combine(dir.FullName, "reference")),
                f => Path.GetFileName(f).Contains('/'));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
