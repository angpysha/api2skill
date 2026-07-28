using System.Text.Json;
using Api2Skill.Model;
using Api2Skill.Output;

namespace Api2Skill.Tests.Output;

/// <summary>T006: legacy <c>specSource</c> → <c>specSources[]</c> load + v2 serialize round-trip.</summary>
public class CompositeManifestTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "api2skill-composite-manifest-" + Guid.NewGuid().ToString("N"));

    public CompositeManifestTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TryLoad_LegacySingularSpecSource_SynthesizesOnePart()
    {
        File.WriteAllText(Path.Combine(_dir, SkillManifestIo.FileName), """
            {
              "name": "petstore",
              "specSource": "./petstore.json",
              "scriptKind": "cs",
              "include": [],
              "exclude": [],
              "format": null,
              "baseUrl": null,
              "insecure": false
            }
            """);

        var loaded = SkillManifestIo.TryLoad(_dir);
        Assert.NotNull(loaded);
        Assert.Equal("./petstore.json", loaded.SpecSource);
        var parts = loaded.GetSpecSources();
        Assert.Single(parts);
        Assert.Equal("petstore", parts[0].Id);
        Assert.Equal("./petstore.json", parts[0].Source);
    }

    [Fact]
    public void Serialize_ThenTryLoad_RoundTripsMultipleSpecSources()
    {
        var manifest = new SkillManifest(
            Name: "acme",
            SpecSource: "./public.json",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Format: null,
            BaseUrl: "https://api.example.com",
            Insecure: false,
            SpecSources:
            [
                new SpecPartManifest("public", "./public.json", "Public API"),
                new SpecPartManifest("admin", "./admin.json", "Admin API"),
            ]);

        var json = SkillManifestIo.Serialize(manifest);
        File.WriteAllText(Path.Combine(_dir, SkillManifestIo.FileName), json);

        using (var doc = JsonDocument.Parse(json))
        {
            Assert.True(doc.RootElement.TryGetProperty("specSources", out var arr));
            Assert.Equal(2, arr.GetArrayLength());
            Assert.Equal("public", arr[0].GetProperty("id").GetString());
            Assert.Equal("Public API", arr[0].GetProperty("label").GetString());
        }

        var loaded = SkillManifestIo.TryLoad(_dir);
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.GetSpecSources().Count);
        Assert.Equal("admin", loaded.GetSpecSources()[1].Id);
        Assert.Equal("./admin.json", loaded.GetSpecSources()[1].Source);
        Assert.Equal("https://api.example.com", loaded.BaseUrl);
    }

    [Fact]
    public void Serialize_NeverEmbedsCredentialShapedKeys()
    {
        var manifest = new SkillManifest(
            "acme",
            "./a.json",
            "cs",
            [],
            [],
            null,
            null,
            false,
            [new SpecPartManifest("a", "./a.json"), new SpecPartManifest("b", "./b.json")]);

        var json = SkillManifestIo.Serialize(manifest);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }
}
