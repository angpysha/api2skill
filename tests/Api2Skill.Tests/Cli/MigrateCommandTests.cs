using Api2Skill.Cli;
using Api2Skill.Output;

namespace Api2Skill.Tests.Cli;

public class MigrateCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-migrate-" + Guid.NewGuid().ToString("N"));

    public MigrateCommandTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public async Task NoManifest_ExitsTwo()
    {
        var target = Path.Combine(_workDir, "not-a-skill");
        Directory.CreateDirectory(target);

        var exitCode = await MigrateCommand.RunAsync(target, "py", CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task UnknownScript_ExitsTwo()
    {
        var exitCode = await MigrateCommand.RunAsync(Path.Combine(_workDir, "missing"), "ruby", CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task MigrateCsToPy_PreservesSecrets_AndRewritesDispatcher()
    {
        var skillDir = Path.Combine(_workDir, "skill");
        var generate = new GenerateOptions(
            SpecSource: FixturePath("petstore.json"),
            OutputDirectory: skillDir,
            Name: "petstore",
            ScriptKind: "cs",
            Include: [],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null);
        Assert.Equal(ExitCodes.Success, await GenerateCommand.RunAsync(generate, CancellationToken.None));

        var secretsPath = Path.Combine(skillDir, "secrets.json");
        var secretBytes = "secret-marker"u8.ToArray();
        await File.WriteAllBytesAsync(secretsPath, secretBytes);

        var exitCode = await MigrateCommand.RunAsync(skillDir, "py", CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(skillDir, "scripts", "call.py")));
        Assert.False(File.Exists(Path.Combine(skillDir, "scripts", "call.cs")));
        Assert.Equal(secretBytes, await File.ReadAllBytesAsync(secretsPath));
        var manifest = SkillManifestIo.TryLoad(skillDir);
        Assert.NotNull(manifest);
        Assert.Equal("py", manifest!.ScriptKind);

        var again = await MigrateCommand.RunAsync(skillDir, "py", CancellationToken.None);
        Assert.Equal(ExitCodes.Success, again);
        Assert.Equal(secretBytes, await File.ReadAllBytesAsync(secretsPath));
    }
}
