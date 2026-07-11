using Api2Skill.Cli;

namespace Api2Skill.Tests.Cli;

/// <summary>
/// T037: the exit-code contract (contracts/cli.md) — calls <see cref="GenerateCommand.RunAsync"/>
/// directly (internal, exposed via InternalsVisibleTo) rather than spawning a subprocess per
/// scenario, since none of these need real process isolation.
/// </summary>
public class ExitCodeTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-exitcode-" + Guid.NewGuid().ToString("N"));

    public ExitCodeTests()
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
        string specSource, string? outDir = null, string scriptKind = "cs", bool force = false) => new(
        SpecSource: specSource,
        OutputDirectory: outDir,
        Name: null,
        ScriptKind: scriptKind,
        Include: [],
        Exclude: [],
        Force: force,
        Insecure: false,
        Format: null,
        BaseUrl: null);

    [Fact]
    public async Task InvalidSpec_ExitsOne_AndWritesNoOutputDirectory()
    {
        var badSpec = Path.Combine(_workDir, "bad.json");
        await File.WriteAllTextAsync(badSpec, "{ not valid json");
        var outDir = Path.Combine(_workDir, "out-invalid");

        var exitCode = await GenerateCommand.RunAsync(Options(badSpec, outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.ParseFailure, exitCode);
        Assert.False(Directory.Exists(outDir), "A parse failure must not create any output directory (FR-010, EC-1).");
    }

    [Fact]
    public async Task UnknownScriptKind_ExitsTwo()
    {
        var outDir = Path.Combine(_workDir, "out-usage");

        var exitCode = await GenerateCommand.RunAsync(
            Options(FixturePath("petstore.json"), outDir, scriptKind: "rust"), CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task ExistingOutputDirectory_WithoutForce_ExitsThree()
    {
        var outDir = Path.Combine(_workDir, "out-exists");
        Directory.CreateDirectory(outDir);

        var exitCode = await GenerateCommand.RunAsync(Options(FixturePath("petstore.json"), outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.OutputExists, exitCode);
    }

    [Fact]
    public async Task MissingSpecFile_ExitsFour()
    {
        var outDir = Path.Combine(_workDir, "out-missing");

        var exitCode = await GenerateCommand.RunAsync(
            Options(Path.Combine(_workDir, "does-not-exist.json"), outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.AcquisitionFailure, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task ValidSpec_ExitsZero_AndWritesTheSkill()
    {
        var outDir = Path.Combine(_workDir, "out-ok");

        var exitCode = await GenerateCommand.RunAsync(Options(FixturePath("petstore.json"), outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "SKILL.md")));
    }

    [Fact]
    public async Task EmptySpec_StillExitsZero_WithAMinimalSkillAndAWarning()
    {
        var emptySpec = Path.Combine(_workDir, "empty.json");
        await File.WriteAllTextAsync(emptySpec,
            """{"openapi":"3.0.3","info":{"title":"Empty","version":"1"},"servers":[{"url":"https://example.com"}],"paths":{}}""");
        var outDir = Path.Combine(_workDir, "out-empty");

        var exitCode = await GenerateCommand.RunAsync(Options(emptySpec, outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "SKILL.md")));
        Assert.True(Directory.Exists(Path.Combine(outDir, "scripts")));
    }
}
