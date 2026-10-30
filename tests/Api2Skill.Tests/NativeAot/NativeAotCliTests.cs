using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api2Skill.Tests.NativeAot;

/// <summary>
/// Publishes the CLI with Native AOT for osx-arm64 once, then exercises that binary.
/// </summary>
public sealed class NativeAotCliFixture : IAsyncLifetime
{
    public string Executable { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var root = FindRepoRoot();
        var project = Path.Combine(root, "src", "Api2Skill", "Api2Skill.csproj");
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("publish");
        psi.ArgumentList.Add(project);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add("osx-arm64");
        psi.ArgumentList.Add("-p:PublishAot=true");
        psi.ArgumentList.Add("-p:NuGetAudit=false");
        psi.ArgumentList.Add("--nologo");

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Native AOT publish failed ({process.ExitCode}).{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }

        Executable = Path.Combine(root, "src", "Api2Skill", "bin", "Release", "net10.0", "osx-arm64", "publish", "api2skill");
        if (!File.Exists(Executable))
        {
            throw new FileNotFoundException("Native AOT executable was not produced.", Executable);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Api2Skill", "Api2Skill.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repo from '{AppContext.BaseDirectory}'.");
    }
}

public class NativeAotCliTests : IClassFixture<NativeAotCliFixture>, IDisposable
{
    private static readonly Regex OperationsJson = new(
        @"json\.loads\(r'''\s*(?<json>\[.*?\])\s*'''\)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private readonly NativeAotCliFixture _fixture;
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-aot-" + Guid.NewGuid().ToString("N"));

    public NativeAotCliTests(NativeAotCliFixture fixture)
    {
        _fixture = fixture;
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

    [Fact]
    public async Task Binary_IsMachOArm64()
    {
        var (exit, stdout, stderr) = await RunAsync("file", _fixture.Executable);
        Assert.Equal(0, exit);
        Assert.Contains("Mach-O", stdout + stderr, StringComparison.Ordinal);
        Assert.Contains("arm64", stdout + stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Help_ListsGenerateMigrateAndLogin()
    {
        var (exit, stdout, stderr) = await Cli("--help");
        var text = stdout + stderr;
        Assert.Equal(0, exit);
        Assert.Contains("generate", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("migrate", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("login", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneratePy_WritesDispatcherManifestAndParsableJson()
    {
        var skill = Path.Combine(_workDir, "py");
        var (exit, _, stderr) = await Cli("generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore");
        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(skill, "scripts", "call.py")), stderr);

        var call = await File.ReadAllTextAsync(Path.Combine(skill, "scripts", "call.py"));
        Assert.Contains("getPetById", call, StringComparison.Ordinal);
        Assert.DoesNotContain("grant_type", call, StringComparison.Ordinal);
        Assert.Contains("python3 scripts/call.py", await File.ReadAllTextAsync(Path.Combine(skill, "SKILL.md")), StringComparison.Ordinal);

        var operations = ParseOperations(call);
        Assert.Contains(operations.EnumerateArray(), op => op.GetProperty("id").GetString() == "getPetById");
        var getPet = operations.EnumerateArray().First(op => op.GetProperty("id").GetString() == "getPetById");
        Assert.Equal("GET", getPet.GetProperty("method").GetString());
        Assert.Contains("petId", getPet.GetProperty("parameters").ToString(), StringComparison.Ordinal);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(skill, ".api2skill.json")));
        Assert.Equal("py", manifest.RootElement.GetProperty("scriptKind").GetString());
        Assert.Equal("petstore", manifest.RootElement.GetProperty("name").GetString());

        using var example = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(skill, "secrets.example.json")));
        Assert.Equal(JsonValueKind.Object, example.RootElement.ValueKind);

        foreach (var schema in Directory.EnumerateFiles(Path.Combine(skill, "reference"), "*.json", SearchOption.AllDirectories))
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(schema));
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        var compile = await RunAsync("python3", "-m", "py_compile", Path.Combine(skill, "scripts", "call.py"));
        Assert.True(compile.Exit == 0, compile.Stderr);
    }

    [Theory]
    [InlineData("cs", "call.cs")]
    [InlineData("fsx", "call.fsx")]
    [InlineData("csx", "call.csx")]
    [InlineData("py", "call.py")]
    public async Task Generate_EachScriptKind_WritesDispatcher(string kind, string fileName)
    {
        var skill = Path.Combine(_workDir, kind);
        var (exit, _, stderr) = await Cli("generate", Fixture("petstore.json"), "--script", kind, "--out", skill, "--name", "petstore");
        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(skill, "scripts", fileName)), stderr);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(skill, ".api2skill.json")));
        Assert.Equal(kind, manifest.RootElement.GetProperty("scriptKind").GetString());
    }

    [Fact]
    public async Task GenerateYaml_Succeeds()
    {
        var skill = Path.Combine(_workDir, "yaml");
        var (exit, _, stderr) = await Cli("generate", Fixture("petstore.yaml"), "--script", "py", "--out", skill, "--name", "petstore");
        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(skill, "scripts", "call.py")), stderr);
    }

    [Fact]
    public async Task Generate_UnknownScript_ExitsUsage()
    {
        var (exit, _, stderr) = await Cli("generate", Fixture("petstore.json"), "--script", "ruby", "--out", Path.Combine(_workDir, "bad"), "--name", "petstore");
        Assert.Equal(2, exit);
        Assert.Contains("py", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_MissingSpec_ExitsNonZero()
    {
        var (exit, _, _) = await Cli("generate", Path.Combine(_workDir, "missing.json"), "--out", Path.Combine(_workDir, "out"), "--name", "missing");
        Assert.NotEqual(0, exit);
    }

    [Fact]
    public async Task Generate_ExistingOutputWithoutForce_ExitsThree()
    {
        var skill = Path.Combine(_workDir, "exists");
        Assert.Equal(0, (await Cli("generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore")).Exit);
        var again = await Cli("generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore");
        Assert.Equal(3, again.Exit);
    }

    [Fact]
    public async Task Generate_Force_Rewrites()
    {
        var skill = Path.Combine(_workDir, "force");
        Assert.Equal(0, (await Cli("generate", Fixture("petstore.json"), "--script", "cs", "--out", skill, "--name", "petstore")).Exit);
        var again = await Cli("generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore", "--force");
        Assert.Equal(0, again.Exit);
        Assert.True(File.Exists(Path.Combine(skill, "scripts", "call.py")));
        Assert.False(File.Exists(Path.Combine(skill, "scripts", "call.cs")));
    }

    [Fact]
    public async Task Generate_IncludeOneOperation_DropsTheOthersFromDispatcher()
    {
        var skill = Path.Combine(_workDir, "include");
        var (exit, _, stderr) = await Cli(
            "generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore",
            "--include", "op:getPetById");
        Assert.Equal(0, exit);
        var operations = ParseOperations(await File.ReadAllTextAsync(Path.Combine(skill, "scripts", "call.py")));
        var ids = operations.EnumerateArray().Select(op => op.GetProperty("id").GetString()).ToArray();
        Assert.Single(ids);
        Assert.Equal("getPetById", ids[0]);
    }

    [Fact]
    public async Task Generate_BaseUrlAndInsecure_AreEmbeddedAsJson()
    {
        var skill = Path.Combine(_workDir, "flags");
        var (exit, _, stderr) = await Cli(
            "generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore",
            "--base-url", "https://pets.example/v2", "--insecure");
        Assert.Equal(0, exit);
        var call = await File.ReadAllTextAsync(Path.Combine(skill, "scripts", "call.py"));
        Assert.Contains("https://pets.example/v2", call, StringComparison.Ordinal);
        Assert.Contains("INSECURE_DEFAULT = \"1\"", call, StringComparison.Ordinal);
        Assert.True(exit == 0, stderr);
    }

    [Fact]
    public async Task Generate_QuotedSchemaProperty_IsEscapedInReference()
    {
        var spec = Path.Combine(_workDir, "quotes.json");
        await File.WriteAllTextAsync(spec, """
            {
              "openapi": "3.0.3",
              "info": { "title": "Quotes", "version": "1.0.0" },
              "paths": {
                "/widgets": {
                  "post": {
                    "operationId": "createWidget",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "schema": {
                            "type": "object",
                            "properties": { "a\"b": { "type": "string" } }
                          }
                        }
                      }
                    },
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """);
        var skill = Path.Combine(_workDir, "quotes-skill");
        var (exit, _, stderr) = await Cli("generate", spec, "--script", "py", "--out", skill, "--name", "quotes");
        Assert.Equal(0, exit);
        var docs = string.Concat(Directory.EnumerateFiles(skill, "*.md", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("a\\u0022b", docs, StringComparison.Ordinal);
        Assert.True(exit == 0, stderr);
    }

    [Fact]
    public async Task Migrate_CsToPy_PreservesSecrets()
    {
        var skill = Path.Combine(_workDir, "migrate");
        Assert.Equal(0, (await Cli("generate", Fixture("petstore.json"), "--script", "cs", "--out", skill, "--name", "petstore")).Exit);
        var secrets = Path.Combine(skill, "secrets.json");
        await File.WriteAllTextAsync(secrets, """{"token":"keep-me"}""");

        var (exit, _, stderr) = await Cli("migrate", skill, "--script", "py");
        Assert.Equal(0, exit);
        Assert.Equal("""{"token":"keep-me"}""", await File.ReadAllTextAsync(secrets));
        Assert.True(File.Exists(Path.Combine(skill, "scripts", "call.py")), stderr);
        Assert.False(File.Exists(Path.Combine(skill, "scripts", "call.cs")));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(skill, ".api2skill.json")));
        Assert.Equal("py", manifest.RootElement.GetProperty("scriptKind").GetString());
    }

    [Fact]
    public async Task Migrate_SameKind_DoesNotRewriteSecrets()
    {
        var skill = Path.Combine(_workDir, "same");
        Assert.Equal(0, (await Cli("generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore")).Exit);
        var secrets = Path.Combine(skill, "secrets.json");
        await File.WriteAllTextAsync(secrets, "same-kind");
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(skill, "scripts", "call.py"));

        var (exit, stdout, _) = await Cli("migrate", skill, "--script", "py");
        Assert.Equal(0, exit);
        Assert.Contains("already", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("same-kind", await File.ReadAllTextAsync(secrets));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path.Combine(skill, "scripts", "call.py")));
    }

    [Fact]
    public async Task Migrate_MissingSkill_ExitsUsage()
    {
        var (exit, _, _) = await Cli("migrate", Path.Combine(_workDir, "nope"), "--script", "py");
        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task Migrate_UnknownScript_ExitsUsage()
    {
        var skill = Path.Combine(_workDir, "unk");
        Assert.Equal(0, (await Cli("generate", Fixture("petstore.json"), "--script", "cs", "--out", skill, "--name", "petstore")).Exit);
        var (exit, _, stderr) = await Cli("migrate", skill, "--script", "ruby");
        Assert.Equal(2, exit);
        Assert.Contains("py", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_KeepsPythonScriptKind()
    {
        var skill = Path.Combine(_workDir, "update");
        Assert.Equal(0, (await Cli("generate", Fixture("petstore.json"), "--script", "py", "--out", skill, "--name", "petstore")).Exit);
        var (exit, _, stderr) = await Cli("update", skill);
        Assert.Equal(0, exit);
        Assert.True(File.Exists(Path.Combine(skill, "scripts", "call.py")), stderr);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(skill, ".api2skill.json")));
        Assert.Equal("py", manifest.RootElement.GetProperty("scriptKind").GetString());
    }

    [Fact]
    public async Task MultiSpec_GeneratesOnePythonDispatcher()
    {
        var skill = Path.Combine(_workDir, "multi");
        var (exit, _, stderr) = await Cli(
            "generate",
            "--spec", Fixture(Path.Combine("multi-spec", "public.json")), "--spec-id", "public",
            "--spec", Fixture(Path.Combine("multi-spec", "billing.json")), "--spec-id", "billing",
            "--on-conflict", "prefix",
            "--base-url", "https://example.com",
            "--script", "py",
            "--out", skill,
            "--name", "combo");
        Assert.Equal(0, exit);
        var call = await File.ReadAllTextAsync(Path.Combine(skill, "scripts", "call.py"));
        var operations = ParseOperations(call);
        Assert.True(operations.GetArrayLength() > 1, stderr);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(skill, ".api2skill.json")));
        Assert.True(manifest.RootElement.GetProperty("specSources").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Login_MissingSkill_ExitsUsage()
    {
        var (exit, _, stderr) = await Cli("login");
        Assert.NotEqual(0, exit);
        Assert.Contains("--skill", stderr, StringComparison.Ordinal);
    }

    private async Task<(int Exit, string Stdout, string Stderr)> Cli(params string[] args) =>
        await RunAsync(_fixture.Executable, args);

    private static async Task<(int Exit, string Stdout, string Stderr)> RunAsync(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout, stderr);
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static JsonElement ParseOperations(string callPy)
    {
        var match = OperationsJson.Match(callPy);
        Assert.True(match.Success, "call.py did not contain an operations JSON array.");
        using var doc = JsonDocument.Parse(match.Groups["json"].Value);
        return doc.RootElement.Clone();
    }
}
