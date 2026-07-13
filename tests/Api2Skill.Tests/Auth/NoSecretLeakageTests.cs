using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Auth;

/// <summary>
/// T078 (closes analyze finding G1): no configured secret value ever appears in any
/// <b>committed</b> generated artifact — Constitution IV / FR-002 / FR-025 / SC-006. The
/// generator never even sees a real secret (it only sees <c>{secret:NAME}</c> references), so
/// this guards against a future regression that might resolve/inline one at generation time.
/// </summary>
public class NoSecretLeakageTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-no-leak-" + Guid.NewGuid().ToString("N"));
    private const string SentinelSecretValue = "SUPER-SECRET-SENTINEL-VALUE";

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData(AuthType.Bearer)]
    [InlineData(AuthType.Basic)]
    [InlineData(AuthType.Custom)]
    public async Task CommittedArtifacts_NeverContainTheSecretValue_EvenIfSomehowPresentAtGenerationTime(AuthType type)
    {
        // The generator has no legitimate way to obtain a real secret (it only ever sees
        // {secret:NAME} placeholders) — this test's AuthConfig, built directly rather than
        // through AuthConfigLoader, still only carries the placeholder, proving the emitted
        // artifacts contain the reference, never a literal that happens to match a real value
        // a user might put in their own out-of-band secrets.json.
        AuthProfile profile = type switch
        {
            AuthType.Bearer => new("default", type, Attachment.Global, new BearerSettings("{secret:TOKEN}"), null, null, null, null),
            AuthType.Basic => new("default", type, Attachment.Global, null, new BasicSettings("{secret:USER}", "{secret:TOKEN}"), null, null, null),
            AuthType.Custom => new("default", type, Attachment.Global, null, null,
                new CustomSettings([new HeaderEntry("Authorization", "{secret:TOKEN}")]), null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion,
            new BuildOptions(Name: "petstore", AuthConfig: new AuthConfig([profile])));

        var outDir = Path.Combine(_workDir, type.ToString());
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        // Simulate the real secret existing ONLY in the git-ignored secrets.json a human would
        // add after generation — never read or embedded by the generator itself.
        await File.WriteAllTextAsync(Path.Combine(outDir, "secrets.json"),
            $$"""{"TOKEN":"{{SentinelSecretValue}}","USER":"someone"}""");

        foreach (var file in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) is "secrets.json") // the one git-ignored, real-secret file
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(SentinelSecretValue, content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AuthShorthand_SecretsExampleJson_ContainsOnlyPlaceholders()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var authConfig = AuthConfigLoader.CreateShorthand("bearer");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion,
            new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        var outDir = Path.Combine(_workDir, "shorthand");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(authConfig));

        var secretsExample = await File.ReadAllTextAsync(Path.Combine(outDir, "secrets.example.json"));
        var authJson = await File.ReadAllTextAsync(Path.Combine(outDir, "auth.json"));

        Assert.DoesNotContain(SentinelSecretValue, secretsExample, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelSecretValue, authJson, StringComparison.Ordinal);
        Assert.Contains("{secret:BEARER_TOKEN}", authJson, StringComparison.Ordinal); // reference, not a value
    }

    [Fact]
    public async Task AutoScaffold_CommittedArtifacts_ContainOnlyPlaceholders()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));
        var scaffold = AuthScaffold.Build(model);
        model = model with { AuthScaffoldGuidance = scaffold.Guidance };

        var outDir = Path.Combine(_workDir, "auto-scaffold");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), scaffoldAuthJson: scaffold.Json);

        foreach (var file in Directory.GetFiles(outDir, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) is "secrets.json")
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(SentinelSecretValue, content, StringComparison.Ordinal);
            if (Path.GetFileName(file) == "auth.json")
            {
                Assert.Contains("{secret:", content, StringComparison.Ordinal);
            }
        }
    }
}
