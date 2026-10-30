using System.Diagnostics;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

public class PyEmitterTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-py-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Emit_WritesCallPy_WithoutOAuthAcquisition()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));

        var dir = new DirectoryInfo(_workDir);
        new PyEmitter().Emit(model, dir);

        var call = await File.ReadAllTextAsync(Path.Combine(_workDir, "scripts", "call.py"));
        var requirements = await File.ReadAllTextAsync(Path.Combine(_workDir, "scripts", "requirements.txt"));

        Assert.Contains("python3 call.py", call, StringComparison.Ordinal);
        Assert.Contains("ensure_deps", call, StringComparison.Ordinal);
        Assert.Contains("getPetById", call, StringComparison.Ordinal);
        Assert.DoesNotContain("grant_type", call, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneratePkce", call, StringComparison.Ordinal);
        Assert.Contains(".pydeps", requirements, StringComparison.Ordinal);

        var compile = Process.Start(new ProcessStartInfo("python3")
        {
            ArgumentList = { "-m", "py_compile", Path.Combine(_workDir, "scripts", "call.py") },
            RedirectStandardError = true,
            UseShellExecute = false,
        })!;
        var errors = await compile.StandardError.ReadToEndAsync();
        await compile.WaitForExitAsync();
        Assert.True(compile.ExitCode == 0, errors);
    }
}
