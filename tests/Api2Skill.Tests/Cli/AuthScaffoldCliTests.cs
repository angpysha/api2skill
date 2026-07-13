using Api2Skill.Cli;

namespace Api2Skill.Tests.Cli;

/// <summary>T007–T009: auto-scaffold on generate, skip when no schemes, preserve on --force.</summary>
public class AuthScaffoldCliTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-scaffold-cli-" + Guid.NewGuid().ToString("N"));

    public AuthScaffoldCliTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static GenerateOptions Options(
        string specSource,
        string? outDir = null,
        bool force = false,
        string[]? include = null,
        string? authConfigPath = null,
        string? authShorthand = null) => new(
        SpecSource: specSource,
        OutputDirectory: outDir,
        Name: null,
        ScriptKind: "cs",
        Include: include ?? [],
        Exclude: [],
        Force: force,
        Insecure: false,
        Format: null,
        BaseUrl: null,
        AuthConfigPath: authConfigPath,
        AuthShorthand: authShorthand,
        Login: false);

    [Fact]
    public async Task Generate_WithoutAuthFlags_WritesAuthJsonWhenSpecHasSchemes()
    {
        var outDir = Path.Combine(_workDir, "scaffold");
        var exitCode = await GenerateCommand.RunAsync(
            Options(FixturePath("multi-auth.yaml"), outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "auth.json")));
        var authJson = await File.ReadAllTextAsync(Path.Combine(outDir, "auth.json"));
        Assert.Contains("_guidance", authJson, StringComparison.Ordinal);
        Assert.Contains("bearerAuth", authJson, StringComparison.Ordinal);

        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.Contains("## Auth profile names", skillMd, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_AgainstAuthLessFilteredSpec_WritesNoAuthJson()
    {
        var outDir = Path.Combine(_workDir, "no-scaffold");
        var exitCode = await GenerateCommand.RunAsync(
            Options(FixturePath("petstore.json"), outDir, include: ["path:/health"]), CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.False(File.Exists(Path.Combine(outDir, "auth.json")));
    }

    [Fact]
    public async Task Generate_ForceWithExistingAuthJson_PreservesBytes()
    {
        var outDir = Path.Combine(_workDir, "preserve");
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            Options(FixturePath("multi-auth.yaml"), outDir), CancellationToken.None));

        var authPath = Path.Combine(outDir, "auth.json");
        var originalBytes = await File.ReadAllBytesAsync(authPath);
        await File.WriteAllTextAsync(authPath, """{"profiles":[{"name":"custom","type":"bearer","token":"{secret:KEPT}"}]}""");

        var editedBytes = await File.ReadAllBytesAsync(authPath);
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(
            Options(FixturePath("multi-auth.yaml"), outDir, force: true), CancellationToken.None));

        var preservedBytes = await File.ReadAllBytesAsync(authPath);
        Assert.Equal(editedBytes, preservedBytes);
        Assert.NotEqual(originalBytes, preservedBytes);
    }
}
