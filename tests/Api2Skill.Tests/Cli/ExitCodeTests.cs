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

    [Theory]
    [InlineData(new[] { "tag:pet,tag:store" }, new[] { "tag:pet", "tag:store" })]
    [InlineData(new[] { "tag:pet", "tag:store" }, new[] { "tag:pet", "tag:store" })]
    [InlineData(new[] { "tag:pet, tag:store" }, new[] { "tag:pet", "tag:store" })]
    [InlineData(new string[0], new string[0])]
    public void SplitSelectors_AcceptsBothCommaSeparatedAndRepeatedFlagForms(string[] raw, string[] expected)
    {
        // contracts/cli.md documents both `--include a,b` and `--include a --include b`;
        // this is a regression guard for the gap where only the repeated-flag form actually
        // worked (comma-joined values were treated as one literal, unmatchable selector).
        Assert.Equal(expected, GenerateCommand.SplitSelectors(raw));
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

    [Fact]
    public async Task UnreachableUrl_ExitsFour_AndWritesNoOutputDirectory()
    {
        // EC-8/contracts/cli.md: input-acquisition failure over HTTP (not just a missing local
        // file, already covered by MissingSpecFile_ExitsFour) must map to the same documented
        // exit code 4, not an unhandled exception or a different code. A closed loopback port
        // gives a fast, reliable "connection refused" without depending on external network
        // access or TLS setup (see UrlFetchTlsTests for why the TLS variant is a manual check).
        using var probe = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
        probe.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var closedPort = ((System.Net.IPEndPoint)probe.LocalEndPoint!).Port;
        probe.Close(); // never listened on — guaranteed connection-refused on this port.

        var outDir = Path.Combine(_workDir, "out-unreachable");

        var exitCode = await GenerateCommand.RunAsync(
            Options($"http://127.0.0.1:{closedPort}/spec.json", outDir), CancellationToken.None);

        Assert.Equal(ExitCodes.AcquisitionFailure, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task IncludeSelectorMatchingNothing_StillExitsZero_WithAMinimalSkillAndAWarning()
    {
        // CLI-level companion to Model/FilterTests.Include_SelectorWithNoMatches_ProducesEmptyModel:
        // confirms the *whole pipeline* (not just SkillModelBuilder in isolation) treats a
        // no-match filter as a valid, if pointless, generation rather than a usage error.
        var outDir = Path.Combine(_workDir, "out-filtered-empty");
        var options = Options(FixturePath("petstore.json"), outDir) with { Include = ["tag:does-not-exist"] };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "SKILL.md")));
    }

    // --- Explicit auth (T028/T043): contracts/cli.md exit codes ---

    [Fact]
    public async Task AuthAndAuthConfigTogether_ExitsTwo()
    {
        var outDir = Path.Combine(_workDir, "out-auth-conflict");
        var options = Options(FixturePath("petstore.json"), outDir) with { AuthConfigPath = "auth.json", AuthShorthand = "bearer" };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task AuthOAuth2Shorthand_ExitsTwo()
    {
        var outDir = Path.Combine(_workDir, "out-auth-oauth2-shorthand");
        var options = Options(FixturePath("petstore.json"), outDir) with { AuthShorthand = "oauth2" };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task MissingAuthConfigFile_ExitsFour()
    {
        var outDir = Path.Combine(_workDir, "out-auth-missing");
        var options = Options(FixturePath("petstore.json"), outDir)
            with
            { AuthConfigPath = Path.Combine(_workDir, "does-not-exist-auth.json") };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.AcquisitionFailure, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task MalformedAuthConfig_ExitsFive_AndWritesNoOutputDirectory()
    {
        var authConfigPath = Path.Combine(_workDir, "bad-auth.json");
        await File.WriteAllTextAsync(authConfigPath, "{ not valid json");
        var outDir = Path.Combine(_workDir, "out-auth-malformed");
        var options = Options(FixturePath("petstore.json"), outDir) with { AuthConfigPath = authConfigPath };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.AuthConfigError, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task AuthProfileHeaderCollision_ExitsFive_AndWritesNoOutputDirectory()
    {
        var authConfigPath = Path.Combine(_workDir, "collision-auth.json");
        await File.WriteAllTextAsync(authConfigPath, """
            { "profiles": [
              { "name": "a", "type": "bearer", "token": "{secret:A}" },
              { "name": "b", "type": "custom", "headers": [ { "name": "Authorization", "value": "{secret:B}" } ] }
            ] }
            """);
        var outDir = Path.Combine(_workDir, "out-auth-collision");
        var options = Options(FixturePath("petstore.json"), outDir) with { AuthConfigPath = authConfigPath };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.AuthConfigError, exitCode);
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task AuthBearerShorthand_ExitsZero_AndWritesAuthJsonAndScaffoldedSecret()
    {
        var outDir = Path.Combine(_workDir, "out-auth-bearer");
        var options = Options(FixturePath("petstore.json"), outDir) with { AuthShorthand = "bearer" };

        var exitCode = await GenerateCommand.RunAsync(options, CancellationToken.None);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "auth.json")));
        var secretsExample = await File.ReadAllTextAsync(Path.Combine(outDir, "secrets.example.json"));
        Assert.Contains("BEARER_TOKEN", secretsExample, StringComparison.Ordinal);
    }
}
