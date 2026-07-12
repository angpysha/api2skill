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

    /// <summary>T007/US1 (specs/004-skill-rename-move-on-update): rename-only preserves credential/cache files in place.</summary>
    [Fact]
    public async Task Update_WithNameOnly_PreservesSecretsAuthJsonAndTokenCache()
    {
        var skillDir = await GenerateWithNonDefaultOptionsAsync(FixturePath("petstore.json"));
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"), """{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""");
        await File.WriteAllTextAsync(Path.Combine(skillDir, "auth.json"), """{"profiles":[{"name":"default","type":"bearer"}]}""");
        await File.WriteAllTextAsync(Path.Combine(skillDir, ".auth-cache.json"), """{"aad":{"access_token":"LIVE-TOKEN"}}""");
        var newSpecPath = await CreateMutatedSpecAsync();

        var exitCode = await UpdateCommand.RunAsync(skillDir, newSpecPath, CancellationToken.None, newName: "mypets-v2");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("""{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""", await File.ReadAllTextAsync(Path.Combine(skillDir, "secrets.json")));
        Assert.Equal("""{"profiles":[{"name":"default","type":"bearer"}]}""", await File.ReadAllTextAsync(Path.Combine(skillDir, "auth.json")));
        Assert.Equal("""{"aad":{"access_token":"LIVE-TOKEN"}}""", await File.ReadAllTextAsync(Path.Combine(skillDir, ".auth-cache.json")));
        var manifest = SkillManifestIo.TryLoad(skillDir);
        Assert.Equal("mypets-v2", manifest!.Name);
    }

    /// <summary>T011/US2: --out moves the skill, preserves credentials at the new location, and removes the old directory.</summary>
    [Fact]
    public async Task Update_WithOut_MovesSkill_PreservesCredentials_AndDeletesSourceDirectory()
    {
        var skillDir = await GenerateWithNonDefaultOptionsAsync(FixturePath("petstore.json"));
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"), """{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""");
        await File.WriteAllTextAsync(Path.Combine(skillDir, "auth.json"), """{"profiles":[{"name":"default","type":"bearer"}]}""");
        await File.WriteAllTextAsync(Path.Combine(skillDir, ".auth-cache.json"), """{"aad":{"access_token":"LIVE-TOKEN"}}""");
        var newSpecPath = await CreateMutatedSpecAsync();
        var newDir = Path.Combine(_workDir, "apis", "petstore-moved");

        var exitCode = await UpdateCommand.RunAsync(skillDir, newSpecPath, CancellationToken.None, newOutputDirectory: newDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.False(Directory.Exists(skillDir)); // old location removed
        Assert.True(File.Exists(Path.Combine(newDir, "scripts", "call.fsx"))); // still fsx — honored from manifest
        Assert.Equal("""{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""", await File.ReadAllTextAsync(Path.Combine(newDir, "secrets.json")));
        Assert.Equal("""{"profiles":[{"name":"default","type":"bearer"}]}""", await File.ReadAllTextAsync(Path.Combine(newDir, "auth.json")));
        Assert.Equal("""{"aad":{"access_token":"LIVE-TOKEN"}}""", await File.ReadAllTextAsync(Path.Combine(newDir, ".auth-cache.json")));
        var manifest = SkillManifestIo.TryLoad(newDir);
        Assert.NotNull(manifest);
        Assert.Equal(newSpecPath, manifest.SpecSource);
    }

    /// <summary>T012/US2: --out normalizing to the same path as skill-path behaves as a plain in-place update.</summary>
    [Fact]
    public async Task Update_WithOutEqualToSkillPath_BehavesAsInPlaceUpdate()
    {
        var skillDir = await GenerateWithNonDefaultOptionsAsync(FixturePath("petstore.json"));
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"), """{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""");
        var newSpecPath = await CreateMutatedSpecAsync();

        // Not textually identical to skillDir, but normalizes (Path.GetFullPath) to the same directory.
        var sameDirDifferentSpelling = Path.Combine(skillDir, "..", Path.GetFileName(skillDir));

        var exitCode = await UpdateCommand.RunAsync(skillDir, newSpecPath, CancellationToken.None, newOutputDirectory: sameDirDifferentSpelling);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(Directory.Exists(skillDir));
        Assert.Equal("""{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""", await File.ReadAllTextAsync(Path.Combine(skillDir, "secrets.json")));
    }

    /// <summary>T015/US3: --name and --out combined in one invocation.</summary>
    [Fact]
    public async Task Update_WithNameAndOut_ProducesRenamedSkillAtNewLocation_WithPreservedCredentials_AndNoSourceDir()
    {
        var skillDir = await GenerateWithNonDefaultOptionsAsync(FixturePath("petstore.json"));
        await File.WriteAllTextAsync(Path.Combine(skillDir, "secrets.json"), """{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""");
        var newSpecPath = await CreateMutatedSpecAsync();
        var newDir = Path.Combine(_workDir, "apis", "renamed");

        var exitCode = await UpdateCommand.RunAsync(
            skillDir, newSpecPath, CancellationToken.None, newName: "renamed", newOutputDirectory: newDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.False(Directory.Exists(skillDir));
        Assert.Equal("""{"apiKeyAuth":{"apiKey":"REAL-SECRET"}}""", await File.ReadAllTextAsync(Path.Combine(newDir, "secrets.json")));
        var manifest = SkillManifestIo.TryLoad(newDir);
        Assert.NotNull(manifest);
        Assert.Equal("renamed", manifest.Name);
        Assert.Equal(newSpecPath, manifest.SpecSource);
    }
}
