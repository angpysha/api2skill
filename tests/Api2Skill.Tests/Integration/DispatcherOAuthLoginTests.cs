using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Integration;

/// <summary>
/// T049/T050/T054/T055 (US3): drives the real generated <c>login &lt;profile&gt;</c> flow as a
/// subprocess against a stub token endpoint. There is no real browser in CI, which is exactly
/// the "headless" edge case (spec US3-6) the dispatcher must handle: it always prints the
/// authorize URL, so the test "plays browser" by extracting <c>state</c> from that printed URL
/// and issuing the redirect itself — a real IdP's redirect looks identical from the dispatcher's
/// perspective.
/// </summary>
[Collection("LoopbackHttp")]
public class DispatcherOAuthLoginTests : IAsyncLifetime
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-oauth-login-" + Guid.NewGuid().ToString("N"));
    private HttpListener _tokenListener = null!;
    private int _tokenPort;
    private int _callbackPort;
    private int _clipboardCallbackPort;
    private int _idTokenCallbackPort;
    private string _skillDir = "";

    public async Task InitializeAsync()
    {
        (_tokenListener, _tokenPort) = LoopbackHttpListenerFactory.Start();
        _callbackPort = GetFreePort();
        _clipboardCallbackPort = GetFreePort();
        _idTokenCallbackPort = GetFreePort();
        _skillDir = await GenerateSkillAsync();
        await File.WriteAllTextAsync(Path.Combine(_skillDir, "secrets.json"), """{"CLIENT_ID":"public-client-id"}""");
    }

    public Task DisposeAsync()
    {
        _tokenListener.Stop();
        _tokenListener.Close();
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private static int GetFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private async Task<string> GenerateSkillAsync()
    {
        var authConfig = new AuthConfig([
            new AuthProfile("aad", AuthType.OAuth2, Attachment.Global, null, null, null, null,
                new OAuthSettings(
                    Grant: OAuthGrant.AuthorizationCode,
                    Preset: null,
                    Tenant: null,
                    AuthUrl: "https://fake-idp.example.com/authorize",
                    TokenUrl: $"http://127.0.0.1:{_tokenPort}/token",
                    Scopes: ["offline_access"],
                    CallbackUrl: $"http://localhost:{_callbackPort}/callback",
                    BrowserLaunch: "auto",
                    ClientAuth: ClientAuthMethod.Body,
                    ClientId: "{secret:CLIENT_ID}",
                    ClientSecret: null,
                    AuthorizeRequest: OAuthRequestExtras.Empty,
                    TokenRequest: OAuthRequestExtras.Empty,
                    TokenField: "access_token")),
            new AuthProfile("aad-clipboard", AuthType.OAuth2, new Attachment(AttachScope.Tags, ["unused-tag"]), null, null, null, null,
                new OAuthSettings(
                    Grant: OAuthGrant.AuthorizationCode,
                    Preset: null,
                    Tenant: null,
                    AuthUrl: "https://fake-idp.example.com/authorize",
                    TokenUrl: $"http://127.0.0.1:{_tokenPort}/token",
                    Scopes: ["offline_access"],
                    CallbackUrl: $"http://localhost:{_clipboardCallbackPort}/callback",
                    BrowserLaunch: "clipboard",
                    ClientAuth: ClientAuthMethod.Body,
                    ClientId: "{secret:CLIENT_ID}",
                    ClientSecret: null,
                    AuthorizeRequest: OAuthRequestExtras.Empty,
                    TokenRequest: OAuthRequestExtras.Empty,
                    TokenField: "access_token")),
            new AuthProfile("aad-id-token", AuthType.OAuth2, new Attachment(AttachScope.Tags, ["unused-tag-2"]), null, null, null, null,
                new OAuthSettings(
                    Grant: OAuthGrant.AuthorizationCode,
                    Preset: null,
                    Tenant: null,
                    AuthUrl: "https://fake-idp.example.com/authorize",
                    TokenUrl: $"http://127.0.0.1:{_tokenPort}/token",
                    Scopes: ["offline_access"],
                    CallbackUrl: $"http://localhost:{_idTokenCallbackPort}/callback",
                    BrowserLaunch: "clipboard",
                    ClientAuth: ClientAuthMethod.Body,
                    ClientId: "{secret:CLIENT_ID}",
                    ClientSecret: null,
                    AuthorizeRequest: OAuthRequestExtras.Empty,
                    TokenRequest: OAuthRequestExtras.Empty,
                    TokenField: "id_token")),
        ]);

        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        var outDir = Path.Combine(_workDir, "skill");
        SkillWriter.Write(model, outDir, force: false, new CsFileEmitter(), AuthConfigLoader.Serialize(authConfig));
        return outDir;
    }

    private Process StartLogin(string profile = "aad")
    {
        var psi = new ProcessStartInfo("dotnet", $"run scripts/call.cs -- login {profile}")
        {
            WorkingDirectory = _skillDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        return Process.Start(psi)!;
    }

    /// <summary>Reads stdout line-by-line until one looks like the printed authorize URL.</summary>
    private static async Task<string> CaptureAuthorizeUrlAsync(Process process)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (line is null)
            {
                break;
            }
            if (line.StartsWith("https://fake-idp.example.com/authorize", StringComparison.Ordinal))
            {
                return line;
            }
        }
        throw new TimeoutException("Did not see the printed authorize URL in the login subprocess's stdout.");
    }

    private static string ExtractQueryParam(string url, string name)
    {
        var query = new Uri(url).Query.TrimStart('?');
        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts[0] == name)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }
        throw new InvalidOperationException($"Query param '{name}' not found in {url}");
    }

    /// <summary>Simulates the IdP's browser redirect by hitting the local callback directly.</summary>
    private Task SendFakeCallbackAsync(string extraQuery) => SendFakeCallbackAsync(extraQuery, _callbackPort);

    private async Task SendFakeCallbackAsync(string extraQuery, int callbackPort)
    {
        await SendFakeCallbackAsync(extraQuery, callbackPort, useLoopbackIp: false);
    }

    private async Task SendFakeCallbackAsync(string extraQuery, int callbackPort, bool useLoopbackIp)
    {
        using var http = new HttpClient();
        var host = useLoopbackIp ? "127.0.0.1" : "localhost";
        // The dispatcher's HttpListener may not have called Start() yet (it starts a beat after
        // the URL is printed) — retry through the brief window.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync($"http://{host}:{callbackPort}/callback?{extraQuery}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return;
            }
            catch (HttpRequestException ex)
            {
                last = ex;
                await Task.Delay(100);
            }
        }
        throw new TimeoutException($"Could not reach the callback listener in time.", last);
    }

    private async Task SendSpuriousCallbackProbeAsync(int callbackPort, string path = "/favicon.ico")
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await http.GetAsync($"http://localhost:{callbackPort}{path}");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                return;
            }
            catch (HttpRequestException ex)
            {
                last = ex;
                await Task.Delay(100);
            }
        }
        throw new TimeoutException($"Could not reach the callback listener in time.", last);
    }

    private async Task RespondToTokenRequestAsync(
        string accessToken = "TESTACCESS123",
        string? refreshToken = "TESTREFRESH456",
        string? idToken = null)
    {
        var context = await _tokenListener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var body = new Dictionary<string, object?> { ["expires_in"] = 3600 };
        if (accessToken is not null)
        {
            body["access_token"] = accessToken;
        }
        if (refreshToken is not null)
        {
            body["refresh_token"] = refreshToken;
        }
        if (idToken is not null)
        {
            body["id_token"] = idToken;
        }
        var json = JsonSerializer.Serialize(body);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
    }

    [Fact]
    public async Task FullLogin_PkceAndStateValid_ExchangesCodeAndStoresToken()
    {
        using var process = StartLogin();
        var authorizeUrl = await CaptureAuthorizeUrlAsync(process);
        var state = ExtractQueryParam(authorizeUrl, "state");
        Assert.Contains("code_challenge=", authorizeUrl, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", authorizeUrl, StringComparison.Ordinal);

        var tokenTask = RespondToTokenRequestAsync();
        await SendFakeCallbackAsync($"code=FAKE_AUTH_CODE&state={Uri.EscapeDataString(state)}");
        await tokenTask;

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var stderr = await process.StandardError.ReadToEndAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("Login succeeded", stderr + await process.StandardOutput.ReadToEndAsync(), StringComparison.Ordinal);

        var cachePath = Path.Combine(_skillDir, ".auth-cache.json");
        Assert.True(File.Exists(cachePath));
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath));
        Assert.Equal("TESTACCESS123", doc.RootElement.GetProperty("aad").GetProperty("access_token").GetString());
        Assert.Equal("TESTREFRESH456", doc.RootElement.GetProperty("aad").GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task SpuriousRequestBeforeCallback_StillCompletesLogin()
    {
        using var process = StartLogin();
        var authorizeUrl = await CaptureAuthorizeUrlAsync(process);
        var state = ExtractQueryParam(authorizeUrl, "state");

        await SendSpuriousCallbackProbeAsync(_callbackPort);

        var tokenTask = RespondToTokenRequestAsync();
        await SendFakeCallbackAsync($"code=AFTER_PROBE&state={Uri.EscapeDataString(state)}");
        await tokenTask;

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task CallbackVia127001_WhenProfileUsesLocalhost_StillCompletesLogin()
    {
        using var process = StartLogin();
        var authorizeUrl = await CaptureAuthorizeUrlAsync(process);
        var state = ExtractQueryParam(authorizeUrl, "state");

        var tokenTask = RespondToTokenRequestAsync();
        await SendFakeCallbackAsync($"code=LOOPBACK_IP&state={Uri.EscapeDataString(state)}", _callbackPort, useLoopbackIp: true);
        await tokenTask;

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task StateMismatch_RejectsCallback_NoTokenStored()
    {
        using var process = StartLogin();
        await CaptureAuthorizeUrlAsync(process);

        await SendFakeCallbackAsync("code=FAKE_AUTH_CODE&state=WRONG_STATE_VALUE");

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var stderr = await process.StandardError.ReadToEndAsync();

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("state did not match", stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_skillDir, ".auth-cache.json")));
    }

    [Fact]
    public async Task HeadlessEnvironment_StillPrintsAuthorizeUrl_AndCompletesOnRedirect()
    {
        // CI is inherently headless — this scenario is exercised by every other test here too,
        // but this test names it explicitly per the spec edge case (US3-6) and asserts the
        // specific guidance line appears.
        using var process = StartLogin();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string? guidanceLine = null;
        while (DateTime.UtcNow < deadline)
        {
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (line is null) break;
            if (line.Contains("visit this URL", StringComparison.Ordinal) || line.Contains("Could not launch a browser", StringComparison.Ordinal))
            {
                guidanceLine = line;
            }
            if (line.StartsWith("https://fake-idp.example.com/authorize", StringComparison.Ordinal))
            {
                var state = ExtractQueryParam(line, "state");
                var tokenTask = RespondToTokenRequestAsync();
                await SendFakeCallbackAsync($"code=X&state={Uri.EscapeDataString(state)}");
                await tokenTask;
                break;
            }
        }

        Assert.NotNull(guidanceLine);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task CallbackPortAlreadyInUse_ReportsConflict()
    {
        // Occupy the callback port ourselves before the dispatcher can bind it.
        using var blocker = new HttpListener();
        blocker.Prefixes.Add($"http://localhost:{_callbackPort}/");
        blocker.Start();
        try
        {
            using var process = StartLogin();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var stderr = await process.StandardError.ReadToEndAsync();

            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains("callback", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            blocker.Stop();
            blocker.Close();
        }
    }

    [Fact]
    public async Task LoginOnUnknownProfile_FailsWithClearMessage()
    {
        using var process = StartLogin("does-not-exist");
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var stderr = await process.StandardError.ReadToEndAsync();

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("does-not-exist", stderr, StringComparison.Ordinal);
    }

    /// <summary>
    /// Spec 005 US1/US3: <c>"browserLaunch": "clipboard"</c> must never launch a browser, must
    /// print the authorize URL either way, and must still complete the callback + token exchange
    /// exactly like the "auto" profile — regardless of whether a clipboard tool is available on
    /// the test runner (CI is a "no clipboard tool" environment, exercising the US3 fallback).
    /// </summary>
    [Fact]
    public async Task ClipboardBrowserLaunch_NeverOpensBrowser_PrintsUrlAndCompletesLogin()
    {
        using var process = StartLogin("aad-clipboard");
        var deadline = DateTime.UtcNow.AddSeconds(30);
        string? deliveryLine = null;
        while (DateTime.UtcNow < deadline)
        {
            var line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
            if (line is null) break;
            Assert.DoesNotContain("Opening your browser", line, StringComparison.Ordinal);
            if (line.Contains("Copied the sign-in URL", StringComparison.Ordinal)
                || line.Contains("Could not copy the sign-in URL", StringComparison.Ordinal))
            {
                deliveryLine = line;
            }
            if (line.StartsWith("https://fake-idp.example.com/authorize", StringComparison.Ordinal))
            {
                var state = ExtractQueryParam(line, "state");
                var tokenTask = RespondToTokenRequestAsync();
                await SendFakeCallbackAsync($"code=CLIP_CODE&state={Uri.EscapeDataString(state)}", _clipboardCallbackPort);
                await tokenTask;
                break;
            }
        }

        Assert.NotNull(deliveryLine);
        Assert.Contains("aad-clipboard", deliveryLine, StringComparison.Ordinal);

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);

        var cachePath = Path.Combine(_skillDir, ".auth-cache.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath));
        Assert.Equal("TESTACCESS123", doc.RootElement.GetProperty("aad-clipboard").GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task TokenFieldIdToken_StoresSelectedBearerAndIdTokenSibling()
    {
        using var process = StartLogin("aad-id-token");
        var authorizeUrl = await CaptureAuthorizeUrlAsync(process);
        var state = ExtractQueryParam(authorizeUrl, "state");

        var tokenTask = RespondToTokenRequestAsync(
            accessToken: "ACCESS-SHOULD-NOT-BE-BEARER",
            refreshToken: "REFRESH-ID",
            idToken: "ID-TOKEN-AS-BEARER");
        await SendFakeCallbackAsync($"code=ID_TOKEN_CODE&state={Uri.EscapeDataString(state)}", _idTokenCallbackPort);
        await tokenTask;

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, process.ExitCode);

        var cachePath = Path.Combine(_skillDir, ".auth-cache.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath));
        var entry = doc.RootElement.GetProperty("aad-id-token");
        Assert.Equal("ID-TOKEN-AS-BEARER", entry.GetProperty("access_token").GetString());
        Assert.Equal("ID-TOKEN-AS-BEARER", entry.GetProperty("id_token").GetString());
        Assert.Equal("REFRESH-ID", entry.GetProperty("refresh_token").GetString());
    }

    [Fact]
    public async Task TokenFieldIdToken_MissingPreferred_FallsBackWithWarning()
    {
        using var process = StartLogin("aad-id-token");
        var authorizeUrl = await CaptureAuthorizeUrlAsync(process);
        var state = ExtractQueryParam(authorizeUrl, "state");

        var tokenTask = RespondToTokenRequestAsync(
            accessToken: "FALLBACK-ACCESS",
            refreshToken: null,
            idToken: null);
        await SendFakeCallbackAsync($"code=FALLBACK_CODE&state={Uri.EscapeDataString(state)}", _idTokenCallbackPort);
        await tokenTask;

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var stderr = await process.StandardError.ReadToEndAsync();

        Assert.Equal(0, process.ExitCode);
        Assert.Contains("missing 'id_token'", stderr, StringComparison.Ordinal);
        Assert.Contains("using 'access_token'", stderr, StringComparison.Ordinal);

        var cachePath = Path.Combine(_skillDir, ".auth-cache.json");
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath));
        var entry = doc.RootElement.GetProperty("aad-id-token");
        Assert.Equal("FALLBACK-ACCESS", entry.GetProperty("access_token").GetString());
        Assert.False(entry.TryGetProperty("id_token", out _));
    }
}
