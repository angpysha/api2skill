using System.Diagnostics;

namespace Api2Skill.Tests.Input;

/// <summary>
/// T028: stdin is non-seekable, which is exactly the case Microsoft.OpenApi's LoadAsync can't
/// handle directly for JSON without buffering first (github.com/Microsoft/OpenAPI.NET#2638,
/// research.md R2/R3) — so this runs the actual CLI as a subprocess with piped input rather
/// than unit-testing SpecSource.AcquireStdinAsync in isolation (Console.OpenStandardInput()
/// isn't meaningfully redirectable within the same test process).
/// </summary>
public class StdinSourceTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-stdin-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static string FindCliProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Api2Skill.slnx")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir!.FullName, "src", "Api2Skill");
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(string stdinContent, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet", $"run --project \"{FindCliProject()}\" -- {string.Join(' ', args)}")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        await process.StandardInput.WriteAsync(stdinContent);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    [Fact]
    public async Task GenerateFromStdin_WithExplicitFormat_Succeeds()
    {
        var content = await File.ReadAllTextAsync(FixturePath("petstore.json"));
        var outDir = Path.Combine(_workDir, "out-json");

        var (exitCode, stdout, stderr) = await RunCliAsync(content, "generate", "-", "-o", outDir, "--format", "json");

        Assert.Equal(0, exitCode);
        Assert.Contains("5 operation(s)", stdout);
        Assert.True(File.Exists(Path.Combine(outDir, "SKILL.md")), stderr);
    }

    [Fact]
    public async Task GenerateFromStdin_SniffsJsonFormat_WithoutExplicitFormatFlag()
    {
        var content = await File.ReadAllTextAsync(FixturePath("petstore.json"));
        var outDir = Path.Combine(_workDir, "out-sniff-json");

        var (exitCode, stdout, _) = await RunCliAsync(content, "generate", "-", "-o", outDir);

        Assert.Equal(0, exitCode);
        Assert.Contains("5 operation(s)", stdout);
    }

    [Fact]
    public async Task GenerateFromStdin_SniffsYamlFormat_WithoutExplicitFormatFlag()
    {
        var content = await File.ReadAllTextAsync(FixturePath("petstore.yaml"));
        var outDir = Path.Combine(_workDir, "out-sniff-yaml");

        var (exitCode, stdout, _) = await RunCliAsync(content, "generate", "-", "-o", outDir);

        Assert.Equal(0, exitCode);
        Assert.Contains("5 operation(s)", stdout);
    }
}
