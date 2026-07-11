using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T008-T010 (specs/003-skill-update-command, US1): a skill generated with non-default options
/// can be refreshed from a new spec via <c>update</c> without re-supplying any of those options,
/// and <c>--force</c>'s existing preservation guarantees still apply.
/// </summary>
public class UpdateCommandIntegrationTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-update-integration-" + Guid.NewGuid().ToString("N"));

    public UpdateCommandIntegrationTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private async Task<string> GenerateWithNonDefaultOptionsAsync(string specPath)
    {
        var skillDir = Path.Combine(_workDir, "skill");
        var options = new GenerateOptions(
            SpecSource: specPath,
            OutputDirectory: skillDir,
            Name: "mypets",
            ScriptKind: "fsx",
            Include: ["tag:pet"],
            Exclude: [],
            Force: false,
            Insecure: true,
            Format: null,
            BaseUrl: "https://staging.example.com");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(options, CancellationToken.None));
        return skillDir;
    }

    /// <summary>Copies the petstore fixture and adds one new pet-tagged operation, simulating a newer spec version.</summary>
    private async Task<string> CreateMutatedSpecAsync()
    {
        var original = await File.ReadAllTextAsync(FixturePath("petstore.json"));
        var mutated = original.Replace(
            "\"paths\": {",
            """
            "paths": {
                "/pet/{petId}/vaccinations": {
                  "get": {
                    "tags": ["pet"],
                    "operationId": "getPetVaccinations",
                    "responses": { "200": { "description": "ok" } }
                  }
                },
            """);
        var path = Path.Combine(_workDir, "petstore-v2.json");
        await File.WriteAllTextAsync(path, mutated);
        return path;
    }

    [Fact]
    public async Task Update_WithNewSpec_HonorsOriginalScriptKindAndFilters_WithoutBeingToldThem()
    {
        var skillDir = await GenerateWithNonDefaultOptionsAsync(FixturePath("petstore.json"));
        var newSpecPath = await CreateMutatedSpecAsync();

        var exitCode = await UpdateCommand.RunAsync(skillDir, newSpecPath, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(skillDir, "scripts", "call.fsx"))); // still fsx
        Assert.False(File.Exists(Path.Combine(skillDir, "scripts", "call.cs"))); // not switched to cs
        var dispatcherText = await File.ReadAllTextAsync(Path.Combine(skillDir, "scripts", "call.fsx"));
        Assert.Contains("getPetVaccinations", dispatcherText, StringComparison.Ordinal); // new op present
        // Only the "pet" tag was included originally — store-tagged ops must still be excluded.
        Assert.DoesNotContain("get_store_inventory", dispatcherText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_PreservesSecretsAndAuthJsonAndTokenCache_LikeForceAlreadyDoes()
    {
        var skillDir = await GenerateWithNonDefaultOptionsAsync(FixturePath("petstore.json"));
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"), """{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""");
        await File.WriteAllTextAsync(Path.Combine(skillDir, ".auth-cache.json"), """{"aad":{"access_token":"LIVE-TOKEN"}}""");
        var newSpecPath = await CreateMutatedSpecAsync();

        var exitCode = await UpdateCommand.RunAsync(skillDir, newSpecPath, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("""{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""", await File.ReadAllTextAsync(Path.Combine(skillDir, "secrets.json")));
        Assert.Equal("""{"aad":{"access_token":"LIVE-TOKEN"}}""", await File.ReadAllTextAsync(Path.Combine(skillDir, ".auth-cache.json")));
    }

    [Fact]
    public async Task Update_WithNoNewSpecSource_ReResolvesTheOriginalRecordedSource()
    {
        // A private copy in _workDir — NEVER mutate the shared fixture under AppContext.BaseDirectory
        // (CopyToOutputDirectory="PreserveNewest" means an in-place edit there could leak into
        // every other test that reads petstore.json for the rest of this test run).
        var specPath = Path.Combine(_workDir, "my-own-spec.json");
        File.Copy(FixturePath("petstore.json"), specPath);
        var skillDir = await GenerateWithNonDefaultOptionsAsync(specPath);

        var mutatedPath = await CreateMutatedSpecAsync();
        File.Copy(mutatedPath, specPath, overwrite: true);

        var exitCode = await UpdateCommand.RunAsync(skillDir, null, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        var dispatcherText = await File.ReadAllTextAsync(Path.Combine(skillDir, "scripts", "call.fsx"));
        Assert.Contains("getPetVaccinations", dispatcherText, StringComparison.Ordinal);

        var manifest = SkillManifestIo.TryLoad(skillDir);
        Assert.Equal(specPath, manifest!.SpecSource); // unchanged — no new source was given
    }
}
