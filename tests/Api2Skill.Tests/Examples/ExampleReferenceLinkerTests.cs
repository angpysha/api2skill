using Api2Skill.Examples;

namespace Api2Skill.Tests.Examples;

public class ExampleReferenceLinkerTests
{
    [Fact]
    public void BuildSection_EmitsRelativeLinks()
    {
        var md = ExampleReferenceLinker.BuildSection(
            "addPet",
            [new ExampleArtifact("addPet", "happy", HasRequest: true, HasResponse: true)]);

        Assert.Contains(ExampleReferenceLinker.SectionHeading, md, StringComparison.Ordinal);
        Assert.Contains("../examples/addPet/happy/request.json", md, StringComparison.Ordinal);
        Assert.Contains("../examples/addPet/happy/response.json", md, StringComparison.Ordinal);
        Assert.Contains("`happy`", md, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncDocument_InsertsBeforeSeparator()
    {
        var input = """
            # pet

            ## addPet

            `POST /pet`

            **Auth**: none

            ---

            ## findPets

            `GET /pet`

            ---

            """;

        var byOp = new Dictionary<string, IReadOnlyList<ExampleArtifact>>(StringComparer.Ordinal)
        {
            ["addPet"] = [new ExampleArtifact("addPet", "happy", true, false)],
        };

        var updated = ExampleReferenceLinker.SyncDocument(input, byOp);

        Assert.Contains("**Authored examples**", updated, StringComparison.Ordinal);
        Assert.Contains("../examples/addPet/happy/request.json", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("findPets/happy", updated, StringComparison.Ordinal);

        var addPetPos = updated.IndexOf("## addPet", StringComparison.Ordinal);
        var authoredPos = updated.IndexOf("**Authored examples**", StringComparison.Ordinal);
        var sepPos = updated.IndexOf("\n---\n", addPetPos, StringComparison.Ordinal);
        Assert.True(authoredPos > addPetPos && authoredPos < sepPos);
    }

    [Fact]
    public void SyncDocument_ReplacesExistingSection()
    {
        var input = """
            ## addPet

            **Authored examples**

            old

            | name | request | response |
            |------|---------|----------|
            | `old` | — | — |

            ---

            """;

        var byOp = new Dictionary<string, IReadOnlyList<ExampleArtifact>>(StringComparer.Ordinal)
        {
            ["addPet"] = [new ExampleArtifact("addPet", "happy", true, false)],
        };

        var updated = ExampleReferenceLinker.SyncDocument(input, byOp);
        Assert.Contains("`happy`", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("`old`", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncSkill_PatchesReferenceFiles()
    {
        var skill = Path.Combine(Path.GetTempPath(), "api2skill-ex-link-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(skill, "reference"));
            File.WriteAllText(Path.Combine(skill, "reference", "pet.md"), """
                # pet

                ## addPet

                `POST /pet`

                ---

                """);

            ExampleStore.Write(skill, "addPet", "happy", """{"name":"doggie"}""", null, force: false);
            var orphans = ExampleReferenceLinker.SyncSkill(skill, new HashSet<string> { "addPet" });
            Assert.Empty(orphans);

            var md = File.ReadAllText(Path.Combine(skill, "reference", "pet.md"));
            Assert.Contains("../examples/addPet/happy/request.json", md, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(skill))
            {
                Directory.Delete(skill, recursive: true);
            }
        }
    }
}
