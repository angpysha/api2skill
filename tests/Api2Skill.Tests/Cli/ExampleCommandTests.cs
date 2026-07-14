using Api2Skill.Cli;
using Api2Skill.Examples;

namespace Api2Skill.Tests.Cli;

public class ExampleCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-ex-cli-" + Guid.NewGuid().ToString("N"));

    public ExampleCommandTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private async Task<string> GeneratePetstoreAsync(string name)
    {
        var outDir = Path.Combine(_workDir, name);
        var exit = await GenerateCommand.RunAsync(
            new GenerateOptions(
                SpecSource: FixturePath("petstore.json"),
                OutputDirectory: outDir,
                Name: null,
                ScriptKind: "cs",
                Include: [],
                Exclude: [],
                Force: false,
                Insecure: false,
                Format: null,
                BaseUrl: null,
                AuthConfigPath: null,
                AuthShorthand: null,
                Login: false),
            CancellationToken.None);
        Assert.Equal(ExitCodes.Success, exit);
        return outDir;
    }

    [Fact]
    public async Task ExampleAdd_WritesFileAndLinksReference()
    {
        var skill = await GeneratePetstoreAsync("add");
        var requestPath = Path.Combine(_workDir, "body.json");
        await File.WriteAllTextAsync(requestPath, """{"name":"doggie","status":"available"}""");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = ExampleCommand.RunAdd(
            skill, "addPet", "happy", requestPath, null, force: false, stdout, stderr);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(Path.Combine(skill, "examples", "addPet", "happy", "request.json")));
        var petMd = await File.ReadAllTextAsync(Path.Combine(skill, "reference", "pet.md"));
        Assert.Contains("examples/addPet/happy/request.json", petMd, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExampleAdd_SecondName_KeepsBoth()
    {
        var skill = await GeneratePetstoreAsync("two");
        var r1 = Path.Combine(_workDir, "r1.json");
        var r2 = Path.Combine(_workDir, "r2.json");
        await File.WriteAllTextAsync(r1, """{"a":1}""");
        await File.WriteAllTextAsync(r2, """{"a":2}""");

        using var sw = new StringWriter();
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunAdd(skill, "addPet", "happy", r1, null, false, sw, sw));
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunAdd(skill, "addPet", "alt", r2, null, false, sw, sw));

        var petMd = await File.ReadAllTextAsync(Path.Combine(skill, "reference", "pet.md"));
        Assert.Contains("`happy`", petMd, StringComparison.Ordinal);
        Assert.Contains("`alt`", petMd, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExampleAdd_UnknownOp_Exit2()
    {
        var skill = await GeneratePetstoreAsync("unknown");
        var req = Path.Combine(_workDir, "u.json");
        await File.WriteAllTextAsync(req, "{}");
        using var sw = new StringWriter();
        var exit = ExampleCommand.RunAdd(skill, "noSuchOp", "default", req, null, false, sw, sw);
        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public async Task ExampleAdd_ExistsWithoutForce_Exit2()
    {
        var skill = await GeneratePetstoreAsync("force");
        var req = Path.Combine(_workDir, "f.json");
        await File.WriteAllTextAsync(req, "{}");
        using var sw = new StringWriter();
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunAdd(skill, "addPet", "happy", req, null, false, sw, sw));
        Assert.Equal(ExitCodes.UsageError, ExampleCommand.RunAdd(skill, "addPet", "happy", req, null, false, sw, sw));
    }

    [Fact]
    public async Task ExampleAdd_MissingSkill_Exit4()
    {
        using var sw = new StringWriter();
        var exit = ExampleCommand.RunAdd(Path.Combine(_workDir, "missing"), "addPet", "happy", null, null, false, sw, sw);
        Assert.Equal(ExitCodes.AcquisitionFailure, exit);
    }

    [Fact]
    public async Task ExampleListRemoveSync_RoundTrip()
    {
        var skill = await GeneratePetstoreAsync("list");
        var req = Path.Combine(_workDir, "l.json");
        await File.WriteAllTextAsync(req, """{"x":1}""");

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunAdd(skill, "addPet", "happy", req, null, false, stdout, stderr));

        stdout.GetStringBuilder().Clear();
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunList(skill, "addPet", stdout, stderr));
        Assert.Contains("addPet\thappy\tTrue\tFalse", stdout.ToString(), StringComparison.Ordinal);

        Assert.Equal(ExitCodes.Success, ExampleCommand.RunSync(skill, stdout, stderr));
        Assert.Equal(ExitCodes.Success, ExampleCommand.RunRemove(skill, "addPet", "happy", stdout, stderr));
        Assert.False(Directory.Exists(Path.Combine(skill, "examples", "addPet", "happy")));

        var petMd = await File.ReadAllTextAsync(Path.Combine(skill, "reference", "pet.md"));
        Assert.DoesNotContain("**Authored examples**", petMd, StringComparison.Ordinal);
    }
}
