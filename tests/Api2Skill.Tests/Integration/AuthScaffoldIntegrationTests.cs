using Api2Skill.Cli;

namespace Api2Skill.Tests.Integration;

/// <summary>T010: scaffold, edit secrets, regenerate with --auth-config succeeds.</summary>
public class AuthScaffoldIntegrationTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-scaffold-int-" + Guid.NewGuid().ToString("N"));

    public AuthScaffoldIntegrationTests()
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

    [Fact]
    public async Task ScaffoldThenActivateWithAuthConfig_Succeeds()
    {
        var outDir = Path.Combine(_workDir, "activate");
        var exitCode = await GenerateCommand.RunAsync(new GenerateOptions(
            SpecSource: FixturePath("multi-auth.yaml"),
            OutputDirectory: outDir,
            Name: "multi-auth",
            ScriptKind: "cs",
            Include: ["op:bearerOp"],
            Exclude: [],
            Force: false,
            Insecure: false,
            Format: null,
            BaseUrl: null,
            AuthConfigPath: null,
            AuthShorthand: null,
            Login: false), CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exitCode);

        var authPath = Path.Combine(outDir, "auth.json");
        Assert.True(File.Exists(authPath));

        await File.WriteAllTextAsync(Path.Combine(outDir, "secrets.json"), """
            {
              "bearerAuth": { "bearerToken": "b" },
              "bearerAuth_TOKEN": "tok"
            }
            """);

        exitCode = await GenerateCommand.RunAsync(new GenerateOptions(
            SpecSource: FixturePath("multi-auth.yaml"),
            OutputDirectory: outDir,
            Name: "multi-auth",
            ScriptKind: "cs",
            Include: ["op:bearerOp"],
            Exclude: [],
            Force: true,
            Insecure: false,
            Format: null,
            BaseUrl: null,
            AuthConfigPath: authPath,
            AuthShorthand: null,
            Login: false), CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        var skillMd = await File.ReadAllTextAsync(Path.Combine(outDir, "SKILL.md"));
        Assert.Contains("## Explicit auth profiles (auth.json)", skillMd, StringComparison.Ordinal);
    }
}
