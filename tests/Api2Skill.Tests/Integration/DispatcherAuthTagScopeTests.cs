using System.Diagnostics;
using System.Net;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T042 (US2): a tag-scoped profile applies only to operations carrying that tag; a global
/// profile applies to every operation; both apply together (stacked) on a covered operation.
/// Uses the petstore fixture, which has real tag diversity (pet / store / default) unlike
/// multi-auth.yaml's single tag.
/// </summary>
[Collection("LoopbackHttp")]
public class DispatcherAuthTagScopeTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-tagscope-" + Guid.NewGuid().ToString("N"));
    private HttpListener _listener = null!;
    private int _port;
    private string _skillDir = "";

    public async Task InitializeAsync()
    {
        (_listener, _port) = LoopbackHttpListenerFactory.Start();

        // "gw" is global (applies to every operation); "storeOnly" is attached to the "store"
        // tag only (petstore's store-inventory operation is the sole "store"-tagged op).
        var authConfig = new AuthConfig([
            new AuthProfile("gw", AuthType.Custom, Attachment.Global, null, null,
                new CustomSettings([new HeaderEntry("ApiKey", "{secret:GW_KEY}")]), null, null),
            new AuthProfile("storeOnly", AuthType.Bearer, new Attachment(AttachScope.Tags, ["store"]),
                new BearerSettings("{secret:STORE_TOKEN}"), null, null, null, null),
        ]);

        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion,
            new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        _skillDir = Path.Combine(_workDir, "skill");
        SkillWriter.Write(model, _skillDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(authConfig));
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), """
            {"GW_KEY":"gwkey123","STORE_TOKEN":"storetok456"}
            """);
    }

    public Task DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunDispatcherAsync(string operationId, params string[] extraArgs)
    {
        var psi = new ProcessStartInfo("dotnet", $"run scripts/call.cs -- {operationId} {string.Join(' ', extraArgs)}")
        {
            WorkingDirectory = _skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["API2SKILL_BASE_URL"] = $"http://127.0.0.1:{_port}";

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private async Task<HttpListenerRequest> CaptureRequestAsync()
    {
        var context = await _listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var request = context.Request;
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}");
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
        return request;
    }

    [Fact]
    public async Task GlobalProfile_AppliesToAnOperationOutsideTheScopedTag()
    {
        // "get_health" carries no tag at all (falls into the "default" bucket) — only the
        // global "gw" profile can reach it.
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("get_health");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("gwkey123", request.Headers["ApiKey"]);
        Assert.Null(request.Headers["Authorization"]); // storeOnly must NOT apply here
    }

    [Fact]
    public async Task GlobalProfile_AppliesToAPetTaggedOperation_TagScopedDoesNot()
    {
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("getPetById", "--petId", "5");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("gwkey123", request.Headers["ApiKey"]);
        Assert.Null(request.Headers["Authorization"]);
    }

    [Fact]
    public async Task TagScopedAndGlobalProfile_BothApplyOnTheMatchingTaggedOperation()
    {
        var serverTask = CaptureRequestAsync();
        var runTask = RunDispatcherAsync("get_store_inventory");

        var request = await serverTask;
        var (exitCode, _, _) = await runTask;

        Assert.Equal(0, exitCode);
        Assert.Equal("gwkey123", request.Headers["ApiKey"]); // global
        Assert.Equal("Bearer storetok456", request.Headers["Authorization"]); // tag-scoped
    }
}
