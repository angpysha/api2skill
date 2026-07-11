using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Auth;

/// <summary>
/// T019: unit tests for per-scheme auth codegen. Asserts on the generated call.cs source
/// text rather than executing it — execution-level coverage (does the emitted code actually
/// apply auth correctly against a real server) lives in Integration/DispatcherAuthTests.
/// </summary>
public class AuthCodegenTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-authcodegen-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<string> GenerateMultiAuthDispatcherAsync() =>
        await GenerateMultiAuthDispatcherAsync(new CsFileEmitter(), "call.cs");

    private static async Task<string> GenerateMultiAuthDispatcherAsync(IScriptEmitter emitter, string scriptFileName)
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("multi-auth.yaml")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "yaml");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "multi-auth"));

        var dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "api2skill-authcodegen-src-" + Guid.NewGuid().ToString("N")));
        try
        {
            emitter.Emit(model, dir);
            return await File.ReadAllTextAsync(Path.Combine(dir.FullName, "scripts", scriptFileName));
        }
        finally
        {
            Directory.Delete(dir.FullName, recursive: true);
        }
    }

    [Fact]
    public async Task Generate_EmitsSchemeSpecForEachSupportedScheme()
    {
        var source = await GenerateMultiAuthDispatcherAsync();

        Assert.Contains("[\"apiKeyAuth\"] = new(\"apiKeyAuth\", \"apiKey\", \"X-Api-Key\", \"header\", null)", source);
        Assert.Contains("[\"bearerAuth\"] = new(\"bearerAuth\", \"bearer\", null, null, null)", source);
        Assert.Contains("[\"basicAuth\"] = new(\"basicAuth\", \"basic\", null, null, null)", source);
        Assert.Contains("[\"oauth2Auth\"] = new(\"oauth2Auth\", \"oauth2\", null, null, \"https://api.multiauth.example.com/oauth/token\")", source);

        // Unsupported scheme (EC-6): still generated as a SchemeSpec so ApplyAuthAsync's
        // `default` warning path is reachable, but the model excludes it from
        // secrets.example.json (SecretsScaffoldTests / no SecretKeys).
        Assert.Contains("[\"openIdAuth\"] = new(\"openIdAuth\", \"unsupported\", null, null, null)", source);
    }

    [Fact]
    public async Task Generate_AppliesAuthBeforeBuildingUrl_SoApiKeyQueryParamsAreIncluded()
    {
        var source = await GenerateMultiAuthDispatcherAsync();

        var authLoopIndex = source.IndexOf("foreach (var schemeId in op.SecuritySchemeIds)", StringComparison.Ordinal);
        var urlBuildIndex = source.IndexOf("var url = baseUrl.TrimEnd", StringComparison.Ordinal);

        Assert.True(authLoopIndex >= 0 && urlBuildIndex >= 0 && authLoopIndex < urlBuildIndex,
            "Auth resolution must run before the URL is assembled, since apiKey auth can add a query parameter.");
    }

    [Fact]
    public async Task Generate_LoadsSecretsRelativeToScriptFile_NotAppContextBaseDirectory()
    {
        // Regression guard for a real bug: AppContext.BaseDirectory resolves to a throwaway
        // dotnet-run build cache for file-based apps, not the script's actual directory.
        var source = await GenerateMultiAuthDispatcherAsync();

        Assert.Contains("[CallerFilePath] string callerPath", source);
        Assert.DoesNotContain("AppContext.BaseDirectory", source);
    }

    [Fact]
    public async Task Generate_NeverEmbedsARealCredential()
    {
        // The generator must never read a real secrets.json during generation (Constitution IV):
        // the emitted source may reference credential *field names* as string keys (e.g.
        // `SecretValue(secrets, scheme.Id, "clientSecret")` — looked up from secrets.json at
        // runtime) and the local *variable* holding that looked-up value (`var clientSecret =
        // ...`), but must never contain a literal *value* resembling a filled-in credential —
        // there is no way it could, since the generator only ever reads the OpenAPI spec.
        var source = await GenerateMultiAuthDispatcherAsync();

        Assert.DoesNotContain("bearerToken\":", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"clientSecret\":\"", source, StringComparison.Ordinal); // a hardcoded JSON value, not the lookup key
        Assert.Contains("SecretValue(secrets, scheme.Id, \"clientSecret\")", source, StringComparison.Ordinal); // looked up at runtime, not embedded
    }

    public static IEnumerable<object[]> AllEmitters()
    {
        yield return [new CsFileEmitter(), "call.cs", "SecretValue(secrets, scheme.Id, \"clientSecret\")"];
        yield return [new CsxEmitter(), "call.csx", "SecretValue(secrets, scheme.Id, \"clientSecret\")"];
        yield return [new FsxEmitter(), "call.fsx", "schemeSecret secrets scheme.Id \"clientSecret\""];
    }

    [Theory]
    [MemberData(nameof(AllEmitters))]
    public async Task Generate_NeverEmbedsARealCredential_AcrossAllThreeEmitters(
        IScriptEmitter emitter, string scriptFileName, string expectedLookupCall)
    {
        // SEC-004 (Phase 8.5 security review): the "no hardcoded credential" guarantee was
        // previously only regression-tested for the .cs emitter, even though FsxEmitter and
        // CsxEmitter independently generate their own auth codegen — a future change to either
        // could silently regress Constitution IV's "never embed a real credential" guarantee
        // with nothing catching it. This runs the same assertions against all three.
        var source = await GenerateMultiAuthDispatcherAsync(emitter, scriptFileName);

        Assert.DoesNotContain("bearerToken\":", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"clientSecret\":\"", source, StringComparison.Ordinal);
        Assert.Contains(expectedLookupCall, source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_CatchesHttpRequestException_ForACleanNetworkErrorMessage()
    {
        // Regression guard: an unreachable server previously crashed with a raw unhandled
        // exception stack trace instead of a clean message + exit code.
        var source = await GenerateMultiAuthDispatcherAsync();

        Assert.Contains("catch (HttpRequestException ex)", source);
        Assert.Contains("return 3;", source);
    }
}
