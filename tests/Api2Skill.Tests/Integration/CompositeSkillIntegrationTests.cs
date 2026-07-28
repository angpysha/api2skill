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

    [Fact]
    public async Task Merge_TwoSkills_SecretsCollision_SourcesUnmodified()
    {
        var pub = Path.Combine(_workDir, "pub-skill");
        var adm = Path.Combine(_workDir, "adm-skill");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("public.json"),
                OutputDirectory: pub,
                Name: "pub",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: "https://api.example.com"),
            CancellationToken.None));
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("admin.json"),
                OutputDirectory: adm,
                Name: "adm",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: "https://api.example.com"),
            CancellationToken.None));

        await File.WriteAllTextAsync(
            Path.Combine(pub, "secrets.json"),
            """{"sharedKey":"from-public","onlyPublic":"p"}""");
        await File.WriteAllTextAsync(
            Path.Combine(adm, "secrets.json"),
            """{"sharedKey":"from-admin","onlyAdmin":"a"}""");

        var pubBefore = await File.ReadAllTextAsync(Path.Combine(pub, SkillManifestIo.FileName));
        var admBefore = await File.ReadAllTextAsync(Path.Combine(adm, SkillManifestIo.FileName));
        var into = Path.Combine(_workDir, "merged");

        var exit = await MergeCommand.RunAsync(into, [pub, adm], force: false, onConflict: "fail", CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exit);

        var secrets = await File.ReadAllTextAsync(Path.Combine(into, "secrets.json"));
        Assert.Contains("from-public", secrets, StringComparison.Ordinal);
        Assert.DoesNotContain("from-admin", secrets, StringComparison.Ordinal);
        Assert.Contains("onlyPublic", secrets, StringComparison.Ordinal);
        Assert.Contains("onlyAdmin", secrets, StringComparison.Ordinal);

        Assert.Equal(pubBefore, await File.ReadAllTextAsync(Path.Combine(pub, SkillManifestIo.FileName)));
        Assert.Equal(admBefore, await File.ReadAllTextAsync(Path.Combine(adm, SkillManifestIo.FileName)));
        Assert.Equal(2, SkillManifestIo.TryLoad(into)!.GetSpecSources().Count);
    }

    [Fact]
    public async Task AddSpec_PreservesAuthAndCache_WhenPreSeeded()
    {
        var skill = Path.Combine(_workDir, "grow");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("public.json"),
                OutputDirectory: skill,
                Name: "grow",
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
            CancellationToken.None));

        var authJson = """{"profiles":[{"name":"seed","type":"bearer","token":"{secret:tok}"}]}""";
        var cacheJson = """{"seed":{"access_token":"cached-token"}}""";
        await File.WriteAllTextAsync(Path.Combine(skill, "auth.json"), authJson);
        await File.WriteAllTextAsync(Path.Combine(skill, ".auth-cache.json"), cacheJson);

        var exit = await AddSpecCommand.RunAsync(
            skill, Fixture("billing.json"), "billing", null, "fail", CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exit);
        Assert.Equal(3, SkillManifestIo.TryLoad(skill)!.GetSpecSources().Count);
        Assert.Equal(authJson, await File.ReadAllTextAsync(Path.Combine(skill, "auth.json")));
        Assert.Equal(cacheJson, await File.ReadAllTextAsync(Path.Combine(skill, ".auth-cache.json")));
    }

    [Fact]
    public async Task Update_MultiPart_RefreshAll_AndSpecIdReplace()
    {
        var skill = Path.Combine(_workDir, "multi-upd");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: Fixture("public.json"),
                OutputDirectory: skill,
                Name: "multi-upd",
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
            CancellationToken.None));

        Assert.Equal(ExitCodes.Success, await UpdateCommand.RunAsync(skill, null, CancellationToken.None));
        Assert.Equal(2, SkillManifestIo.TryLoad(skill)!.GetSpecSources().Count);

        var newAdmin = Path.Combine(_workDir, "admin-v2.json");
        File.Copy(Fixture("admin.json"), newAdmin);
        var bad = await UpdateCommand.RunAsync(skill, newAdmin, CancellationToken.None);
        Assert.Equal(ExitCodes.UsageError, bad);

        var ok = await UpdateCommand.RunAsync(skill, newAdmin, CancellationToken.None, specId: "admin");
        Assert.Equal(ExitCodes.Success, ok);
        var parts = SkillManifestIo.TryLoad(skill)!.GetSpecSources();
        Assert.Equal(2, parts.Count);
        Assert.Equal(newAdmin, parts.First(p => p.Id == "admin").Source);
    }

    [Fact]
    public async Task Update_LegacySingularSpecSource_StillWorks()
    {
        var skill = Path.Combine(_workDir, "legacy");
        var petstore = Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: petstore,
                OutputDirectory: skill,
                Name: "legacy",
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: null),
            CancellationToken.None));

        var escaped = JsonSerializer.Serialize(petstore);
        await File.WriteAllTextAsync(
            Path.Combine(skill, SkillManifestIo.FileName),
            "{\n  \"name\": \"legacy\",\n  \"specSource\": " + escaped + ",\n  \"scriptKind\": \"cs\",\n  \"include\": [],\n  \"exclude\": [],\n  \"format\": null,\n  \"baseUrl\": null,\n  \"insecure\": false\n}\n");

        var exit = await UpdateCommand.RunAsync(skill, null, CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exit);
        Assert.NotNull(SkillManifestIo.TryLoad(skill));
    }
}
