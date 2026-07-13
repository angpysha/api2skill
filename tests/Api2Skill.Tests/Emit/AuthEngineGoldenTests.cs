using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Emit;

/// <summary>
/// T029/T030: golden coverage for the explicit-auth engine text emitted by all three
/// emitters — a content-marker check (rather than a full approved tree, since the
/// no-auth-config path already has full golden trees in <c>CsEmitterGoldenTests</c> /
/// <c>FsxCsxGoldenTests</c>) confirming each emitter renders the bearer/basic/custom handling,
/// the per-operation <c>AuthProfileNames</c> table, and identical behavior across languages
/// (FR-023).
/// </summary>
public class AuthEngineGoldenTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-authengine-golden-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    private static async Task<SkillModel> BuildModelWithBearerProfileAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var authConfig = new AuthConfig([
            new AuthProfile("default", AuthType.Bearer, Attachment.Global, new BearerSettings("{secret:T}"), null, null, null, null),
        ]);
        return SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore", AuthConfig: authConfig));
    }

    [Fact]
    public async Task CsFileEmitter_EmitsExplicitAuthEngineAndProfileNamesTable()
    {
        var model = await BuildModelWithBearerProfileAsync();
        var outDir = Path.Combine(_workDir, "cs");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        Assert.True(File.Exists(Path.Combine(outDir, "auth.json")));
        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.cs"));
        Assert.Contains("LoadAuthConfig", text, StringComparison.Ordinal);
        Assert.Contains("ApplyExplicitProfile", text, StringComparison.Ordinal);
        Assert.Contains("case \"bearer\":", text, StringComparison.Ordinal);
        Assert.Contains("case \"basic\":", text, StringComparison.Ordinal);
        Assert.Contains("case \"custom\":", text, StringComparison.Ordinal);
        Assert.Contains("\"default\"", text, StringComparison.Ordinal); // baked AuthProfileNames entry
        Assert.Contains("AuthResolutionException", text, StringComparison.Ordinal);
        AssertOAuthEngineMarkers_Cs(text);
    }

    [Fact]
    public async Task CsxEmitter_EmitsExplicitAuthEngineAndProfileNamesTable()
    {
        var model = await BuildModelWithBearerProfileAsync();
        var outDir = Path.Combine(_workDir, "csx");
        SkillWriter.Write(model, outDir, force: false, new CsxEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.csx"));
        Assert.Contains("LoadAuthConfig", text, StringComparison.Ordinal);
        Assert.Contains("ApplyExplicitProfileAsync", text, StringComparison.Ordinal);
        Assert.Contains("case \"bearer\":", text, StringComparison.Ordinal);
        Assert.Contains("\"default\"", text, StringComparison.Ordinal);
        AssertOAuthEngineMarkers_Cs(text);
    }

    [Fact]
    public async Task FsxEmitter_EmitsExplicitAuthEngineAndProfileNamesTable()
    {
        var model = await BuildModelWithBearerProfileAsync();
        var outDir = Path.Combine(_workDir, "fsx");
        SkillWriter.Write(model, outDir, force: false, new FsxEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.fsx"));
        Assert.Contains("loadAuthConfig", text, StringComparison.Ordinal);
        Assert.Contains("applyExplicitProfileAsync", text, StringComparison.Ordinal);
        Assert.Contains("\"bearer\" ->", text, StringComparison.Ordinal);
        Assert.Contains("\"basic\" ->", text, StringComparison.Ordinal);
        Assert.Contains("\"custom\" ->", text, StringComparison.Ordinal);
        Assert.Contains("\"default\"", text, StringComparison.Ordinal);
        Assert.Contains("AuthResolutionException", text, StringComparison.Ordinal);
        Assert.Contains("generatePkce", text, StringComparison.Ordinal);
        Assert.Contains("generateState", text, StringComparison.Ordinal);
        Assert.Contains("loginAsync", text, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", text, StringComparison.Ordinal);
        Assert.Contains("withTokenCacheLockAsync", text, StringComparison.Ordinal);
    }

    /// <summary>Shared OAuth2 engine markers for the two C# emitters (cs/csx share near-identical text).</summary>
    private static void AssertOAuthEngineMarkers_Cs(string text)
    {
        Assert.Contains("GeneratePkce", text, StringComparison.Ordinal);
        Assert.Contains("GenerateState", text, StringComparison.Ordinal);
        Assert.Contains("BeginCallbackListener", text, StringComparison.Ordinal);
        Assert.Contains("AwaitOAuthCallbackAsync", text, StringComparison.Ordinal);
        Assert.Contains("LoginAsync", text, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", text, StringComparison.Ordinal);
        Assert.Contains("WithTokenCacheLockAsync", text, StringComparison.Ordinal);
        Assert.Contains("case \"oauth2\":", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoAuthConfig_EmitsNoExplicitAuthEngine_UnchangedFromBaseline()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore"));

        var outDir = Path.Combine(_workDir, "no-auth");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter());

        Assert.False(File.Exists(Path.Combine(outDir, "auth.json")));
        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.cs"));
        // The engine is still emitted (fixed, API-independent per every skill), but every
        // operation's AuthProfileNames is empty, so the spec-derived path is always taken.
        Assert.Contains("if (op.AuthProfileNames.Length > 0)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CsFileEmitter_RunScriptCommandAsync_SetsWorkingDirectoryToSkillRoot()
    {
        var model = await BuildModelWithBearerProfileAsync();
        var outDir = Path.Combine(_workDir, "script-cwd-cs");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.cs"));
        Assert.Contains("RunScriptCommandAsync(string command, string skillRoot)", text, StringComparison.Ordinal);
        Assert.Contains("WorkingDirectory = skillRoot", text, StringComparison.Ordinal);
        Assert.Contains("Path.GetFullPath(Path.Combine(scriptDir, \"..\"))", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CsxEmitter_RunScriptCommandAsync_SetsWorkingDirectoryToSkillRoot()
    {
        var model = await BuildModelWithBearerProfileAsync();
        var outDir = Path.Combine(_workDir, "script-cwd-csx");
        SkillWriter.Write(model, outDir, force: false, new CsxEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.csx"));
        Assert.Contains("RunScriptCommandAsync(string command, string skillRoot)", text, StringComparison.Ordinal);
        Assert.Contains("WorkingDirectory = skillRoot", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FsxEmitter_RunScriptCommandAsync_SetsWorkingDirectoryToSkillRoot()
    {
        var model = await BuildModelWithBearerProfileAsync();
        var outDir = Path.Combine(_workDir, "script-cwd-fsx");
        SkillWriter.Write(model, outDir, force: false, new FsxEmitter(), AuthConfigLoader.Serialize(model.AuthConfig!));

        var text = await File.ReadAllTextAsync(Path.Combine(outDir, "scripts", "call.fsx"));
        Assert.Contains("runScriptCommandAsync (command: string) (skillRoot: string)", text, StringComparison.Ordinal);
        Assert.Contains("psi.WorkingDirectory <- skillRoot", text, StringComparison.Ordinal);
    }
}
