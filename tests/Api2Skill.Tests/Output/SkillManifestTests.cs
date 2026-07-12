using Api2Skill.Output;

namespace Api2Skill.Tests.Output;

/// <summary>T006: <see cref="SkillManifestIo"/> serialize/parse round-trip and failure modes.</summary>
public class SkillManifestTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "api2skill-manifest-" + Guid.NewGuid().ToString("N"));

    public SkillManifestTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Serialize_ThenTryLoad_RoundTripsAllFields()
    {
        var manifest = new SkillManifest(
            Name: "petstore",
            SpecSource: "https://api.example.com/openapi.json",
            ScriptKind: "fsx",
            Include: ["tag:pet", "tag:store"],
            Exclude: ["op:deletePet"],
            Format: "json",
            BaseUrl: "https://staging.example.com",
            Insecure: true);

        File.WriteAllText(Path.Combine(_dir, SkillManifestIo.FileName), SkillManifestIo.Serialize(manifest));
        var loaded = SkillManifestIo.TryLoad(_dir);

        Assert.NotNull(loaded);
        Assert.Equal(manifest.Name, loaded.Name);
        Assert.Equal(manifest.SpecSource, loaded.SpecSource);
        Assert.Equal(manifest.ScriptKind, loaded.ScriptKind);
        Assert.Equal(manifest.Include, loaded.Include);
        Assert.Equal(manifest.Exclude, loaded.Exclude);
        Assert.Equal(manifest.Format, loaded.Format);
        Assert.Equal(manifest.BaseUrl, loaded.BaseUrl);
        Assert.Equal(manifest.Insecure, loaded.Insecure);
    }

    [Fact]
    public void Serialize_DefaultOptions_RoundTripsNullsAndEmptyLists()
    {
        var manifest = new SkillManifest("petstore", "./petstore.json", "cs", [], [], null, null, false);

        File.WriteAllText(Path.Combine(_dir, SkillManifestIo.FileName), SkillManifestIo.Serialize(manifest));
        var loaded = SkillManifestIo.TryLoad(_dir);

        Assert.NotNull(loaded);
        Assert.Empty(loaded.Include);
        Assert.Empty(loaded.Exclude);
        Assert.Null(loaded.Format);
        Assert.Null(loaded.BaseUrl);
        Assert.False(loaded.Insecure);
    }

    [Fact]
    public void TryLoad_NoFile_ReturnsNull()
    {
        Assert.Null(SkillManifestIo.TryLoad(_dir));
    }

    [Fact]
    public void TryLoad_MalformedJson_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_dir, SkillManifestIo.FileName), "{ not valid json");
        Assert.Null(SkillManifestIo.TryLoad(_dir));
    }

    [Fact]
    public void TryLoad_MissingRequiredField_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(_dir, SkillManifestIo.FileName), """{"name":"petstore"}""");
        Assert.Null(SkillManifestIo.TryLoad(_dir));
    }

    [Fact]
    public void Serialize_NeverContainsSecretShapedKeys()
    {
        var manifest = new SkillManifest("petstore", "./petstore.json", "cs", [], [], null, null, false);
        var json = SkillManifestIo.Serialize(manifest);

        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clientSecret", json, StringComparison.OrdinalIgnoreCase);
    }
}
