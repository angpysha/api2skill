using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Api2Skill.Cli;
using Api2Skill.Emit;
using Api2Skill.OAuth;

namespace Api2Skill.Auth;

/// <summary>
/// End-to-end interactive OAuth login for a generated skill directory
/// (<c>api2skill login --skill</c>). Capture is delegated to <see cref="OAuthCaptureCommand"/>;
/// token exchange + <c>.auth-cache.json</c> match dispatcher shape (spec 002 / 009).
/// </summary>
public static class SkillOAuthLogin
{
    private static readonly Regex SecretRef = new(@"^\{secret:([^}]+)\}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public sealed record RunOptions(
        string SkillDir,
        string? ProfileName = null,
        int TimeoutSeconds = 180,
        string? CertPath = null,
        string? CertPassword = null,
        string? CertPemPath = null,
        string? CertKeyPath = null,
        string? RelayBase = null,
        string? Mode = "auto",
        bool IsInteractive = true);

    /// <summary>
    /// Optional overrides for tests: inject capture result and/or HTTP handler for token POST.
    /// </summary>
    public sealed record Hooks(
        Func<Uri, string, int, CancellationToken, Task<CaptureResult>>? CaptureAsync = null,
        HttpMessageHandler? TokenHttpHandler = null,
        Func<string, bool>? TryLaunchBrowser = null,
        Func<string, bool>? TryCopyToClipboard = null);

    public static async Task<int> RunAsync(
        RunOptions options,
        Hooks? hooks = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        CancellationToken cancellationToken = default)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;
        hooks ??= new Hooks();

        var skillDir = Path.GetFullPath(options.SkillDir);
        if (!Directory.Exists(skillDir))
        {
            ConsoleColorWriter.WriteError($"Skill directory not found: {skillDir}", stderr);
            return ExitCodes.AcquisitionFailure;
        }

        var authPath = Path.Combine(skillDir, "auth.json");
        if (!File.Exists(authPath))
        {
            ConsoleColorWriter.WriteError($"auth.json not found in skill directory: {skillDir}", stderr);
            return ExitCodes.AcquisitionFailure;
        }

        AuthConfig authConfig;
        try
        {
            authConfig = AuthConfigLoader.LoadFromFile(authPath, out _);
        }
        catch (Exception ex) when (ex is AuthConfigException or IOException or FileNotFoundException)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.AcquisitionFailure;
        }

        var profile = ResolveProfile(authConfig, options.ProfileName, stderr);
        if (profile is null)
        {
            return ExitCodes.UsageError;
        }

        if (profile.Type != AuthType.OAuth2 || profile.OAuth is null)
        {
            ConsoleColorWriter.WriteError(
                $"Profile '{profile.Name}' is type '{profile.Type}', not 'oauth2' — login is only applicable to oauth2 profiles.",
                stderr);
            return ExitCodes.UsageError;
        }

        var oauth = profile.OAuth;
        if (oauth.Grant == OAuthGrant.ClientCredentials)
        {
            stdout.WriteLine(
                $"Profile '{profile.Name}' uses client_credentials — not applicable: no interactive login is needed, a token is obtained automatically at call time.");
            return ExitCodes.Success;
        }

        if (string.IsNullOrWhiteSpace(oauth.AuthUrl) || string.IsNullOrWhiteSpace(oauth.TokenUrl))
        {
            ConsoleColorWriter.WriteError(
                $"Profile '{profile.Name}' is missing authUrl/tokenUrl (directly or via a preset).",
                stderr);
            return ExitCodes.UsageError;
        }

        var secrets = await LoadSecretsAsync(skillDir, cancellationToken).ConfigureAwait(false);
        string clientId;
        try
        {
            clientId = ResolveSecretOrLiteral(oauth.ClientId, secrets);
        }
        catch (InvalidOperationException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.AcquisitionFailure;
        }

        var callbackUrl = string.IsNullOrWhiteSpace(oauth.CallbackUrl)
            ? "http://localhost:8400/callback"
            : oauth.CallbackUrl;

        var (verifier, challenge) = GeneratePkce();
        var state = GenerateState();
        var authorizeUrl = BuildAuthorizeUrl(oauth, clientId, callbackUrl, challenge, state);

        CaptureResult captureResult;
        if (hooks.CaptureAsync is not null)
        {
            ConsoleColorWriter.WriteInfo($"Listening for OAuth callback on {callbackUrl} …", stderr);
            await PresentAuthorizeUrlAsync(
                profile.Name, authorizeUrl, oauth.BrowserLaunch, hooks, stdout).ConfigureAwait(false);
            captureResult = await hooks.CaptureAsync(
                new Uri(callbackUrl), state, options.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var captureStdout = new StringWriter();
            var captureTask = OAuthCaptureCommand.RunAsync(
                callbackUrl: callbackUrl,
                mode: options.Mode,
                timeoutSeconds: options.TimeoutSeconds,
                certPath: options.CertPath,
                certPassword: options.CertPassword,
                certPemPath: options.CertPemPath,
                certKeyPath: options.CertKeyPath,
                relayBase: options.RelayBase,
                state: state,
                json: true,
                isInteractive: options.IsInteractive,
                stdout: captureStdout,
                stderr: stderr,
                cancellationToken: cancellationToken);

            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            await PresentAuthorizeUrlAsync(
                profile.Name, authorizeUrl, oauth.BrowserLaunch, hooks, stdout).ConfigureAwait(false);

            var captureExit = await captureTask.ConfigureAwait(false);
            var jsonLine = captureStdout.ToString().Trim();
            CaptureResult? parsed = null;
            if (!string.IsNullOrEmpty(jsonLine))
            {
                try
                {
                    parsed = JsonSerializer.Deserialize(jsonLine, CaptureResultJsonContext.Default.CaptureResult);
                }
                catch (JsonException)
                {
                    // fall through
                }
            }

            if (parsed is null)
            {
                ConsoleColorWriter.WriteError(
                    captureExit == ExitCodes.Success
                        ? "oauth-capture produced no parseable JSON result."
                        : $"oauth-capture failed (exit {captureExit}).",
                    stderr);
                return captureExit == ExitCodes.Success ? ExitCodes.UsageError : captureExit;
            }

            captureResult = parsed;
            if (!captureResult.Ok)
            {
                return MapCaptureFailure(captureResult, captureExit, stderr);
            }
        }

        return await FinishLoginAsync(
            skillDir, profile, oauth, secrets, clientId, callbackUrl, verifier, state,
            captureResult, hooks, stdout, stderr, cancellationToken).ConfigureAwait(false);
    }

    private static int MapCaptureFailure(CaptureResult captureResult, int captureExit, TextWriter stderr)
    {
        var err = captureResult.Error ?? "no authorization code was returned";
        ConsoleColorWriter.WriteError($"Login failed: {err}", stderr);
        if (captureExit is ExitCodes.CaptureTimeout or ExitCodes.OAuthRedirectError or ExitCodes.UsageError)
        {
            return captureExit;
        }

        if (string.Equals(err, "timeout", StringComparison.OrdinalIgnoreCase))
        {
            return ExitCodes.CaptureTimeout;
        }

        return string.IsNullOrEmpty(captureResult.Error)
            ? ExitCodes.UsageError
            : ExitCodes.OAuthRedirectError;
    }

    private static async Task<int> FinishLoginAsync(
        string skillDir,
        AuthProfile profile,
        OAuthSettings oauth,
        JsonElement? secrets,
        string clientId,
        string callbackUrl,
        string verifier,
        string state,
        CaptureResult captureResult,
        Hooks hooks,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken)
    {
        if (!captureResult.Ok || string.IsNullOrEmpty(captureResult.Code))
        {
            return MapCaptureFailure(captureResult, ExitCodes.OAuthRedirectError, stderr);
        }

        if (!string.IsNullOrEmpty(captureResult.State) && captureResult.State != state)
        {
            ConsoleColorWriter.WriteError(
                "The callback's state did not match what was sent — rejecting (possible CSRF/mix-up). No token was stored. Run login again.",
                stderr);
            return ExitCodes.UsageError;
        }

        using var http = hooks.TokenHttpHandler is null
            ? new HttpClient()
            : new HttpClient(hooks.TokenHttpHandler, disposeHandler: false);

        var tokenResponse = await ExchangeCodeForTokenAsync(
            http, oauth, clientId, captureResult.Code, verifier, callbackUrl, secrets, cancellationToken)
            .ConfigureAwait(false);
        if (tokenResponse is null)
        {
            ConsoleColorWriter.WriteError("Failed to exchange the authorization code for a token.", stderr);
            return ExitCodes.UsageError;
        }

        var cachePath = Path.Combine(skillDir, SecretsScaffold.TokenCacheFileName);
        await WithTokenCacheLockAsync(cachePath, async () =>
        {
            var cache = await ReadTokenCacheAsync(cachePath, cancellationToken).ConfigureAwait(false);
            StoreTokenInCache(cache, profile.Name, tokenResponse.Value);
            await WriteTokenCacheAsync(cachePath, cache, cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

        stdout.WriteLine(
            $"Login succeeded for profile '{profile.Name}'. You can now call operations that use it.");
        return ExitCodes.Success;
    }

    private static AuthProfile? ResolveProfile(AuthConfig config, string? profileName, TextWriter stderr)
    {
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var match = config.Profiles.FirstOrDefault(p =>
                p.Name.Equals(profileName, StringComparison.Ordinal));
            if (match is null)
            {
                ConsoleColorWriter.WriteError($"No auth profile named '{profileName}' in auth.json.", stderr);
                return null;
            }

            return match;
        }

        var authCode = config.Profiles
            .Where(p => p.Type == AuthType.OAuth2 && p.OAuth?.Grant == OAuthGrant.AuthorizationCode)
            .ToList();
        if (authCode.Count == 1)
        {
            return authCode[0];
        }

        if (authCode.Count == 0)
        {
            ConsoleColorWriter.WriteError(
                "No authorization_code oauth2 profile found in auth.json. Pass --profile <name>.",
                stderr);
            return null;
        }

        ConsoleColorWriter.WriteError(
            "Multiple authorization_code profiles found — pass --profile <name>.",
            stderr);
        return null;
    }

    private static Task PresentAuthorizeUrlAsync(
        string profileName,
        string authorizeUrl,
        string browserLaunch,
        Hooks hooks,
        TextWriter stdout)
    {
        if (string.Equals(browserLaunch, "clipboard", StringComparison.OrdinalIgnoreCase))
        {
            var copied = hooks.TryCopyToClipboard?.Invoke(authorizeUrl) ?? false;
            stdout.WriteLine(copied
                ? $"Copied the sign-in URL to your clipboard for profile '{profileName}'. Paste it into your browser:"
                : $"Could not copy the sign-in URL to the clipboard automatically. Open this URL to sign in for profile '{profileName}':");
            stdout.WriteLine(authorizeUrl);
            return Task.CompletedTask;
        }

        stdout.WriteLine($"Opening your browser to sign in for profile '{profileName}'...");
        var launched = hooks.TryLaunchBrowser?.Invoke(authorizeUrl) ?? TryLaunchBrowserDefault(authorizeUrl);
        stdout.WriteLine(launched
            ? "If it did not open, visit this URL to sign in:"
            : "Could not launch a browser automatically. Open this URL to sign in:");
        stdout.WriteLine(authorizeUrl);
        return Task.CompletedTask;
    }

    private static bool TryLaunchBrowserDefault(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch
        {
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    System.Diagnostics.Process.Start("open", url);
                    return true;
                }

                if (OperatingSystem.IsLinux())
                {
                    System.Diagnostics.Process.Start("xdg-open", url);
                    return true;
                }

                if (OperatingSystem.IsWindows())
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd", $"/c start {url}")
                    {
                        CreateNoWindow = true,
                    });
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }
    }

    private static async Task<JsonElement?> LoadSecretsAsync(string skillDir, CancellationToken ct)
    {
        var path = Path.Combine(skillDir, SecretsScaffold.RealSecretsFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string ResolveSecretOrLiteral(string raw, JsonElement? secrets)
    {
        var m = SecretRef.Match(raw);
        if (!m.Success)
        {
            return raw;
        }

        var name = m.Groups[1].Value;
        if (secrets is { } root && root.TryGetProperty(name, out var el) && el.GetString() is { } value)
        {
            return value;
        }

        throw new InvalidOperationException($"Secret '{name}' is not set in secrets.json.");
    }

    private static (string Verifier, string Challenge) GeneratePkce()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64Url(bytes);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string GenerateState()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Base64Url(bytes);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string BuildAuthorizeUrl(
        OAuthSettings oauth, string clientId, string callbackUrl, string challenge, string state)
    {
        var qs = new List<string>
        {
            "response_type=code",
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(callbackUrl)}",
            $"code_challenge={Uri.EscapeDataString(challenge)}",
            "code_challenge_method=S256",
            $"state={Uri.EscapeDataString(state)}",
        };
        if (oauth.Scopes.Count > 0)
        {
            qs.Add($"scope={Uri.EscapeDataString(string.Join(' ', oauth.Scopes))}");
        }

        foreach (var (key, value) in oauth.AuthorizeRequest.Body)
        {
            qs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        var authUrl = oauth.AuthUrl!;
        var sep = authUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return authUrl + sep + string.Join("&", qs);
    }

    private static async Task<(string AccessToken, string? RefreshToken, int ExpiresIn, string? IdToken)?> ExchangeCodeForTokenAsync(
        HttpClient http,
        OAuthSettings oauth,
        string clientId,
        string code,
        string verifier,
        string callbackUrl,
        JsonElement? secrets,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = callbackUrl,
            ["code_verifier"] = verifier,
            ["client_id"] = clientId,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, oauth.TokenUrl);
        string? clientSecret = null;
        if (!string.IsNullOrEmpty(oauth.ClientSecret))
        {
            try
            {
                clientSecret = ResolveSecretOrLiteral(oauth.ClientSecret, secrets);
            }
            catch (InvalidOperationException)
            {
                clientSecret = null;
            }
        }

        if (oauth.ClientAuth == ClientAuthMethod.Basic && clientSecret is not null)
        {
            form.Remove("client_id");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }
        else if (clientSecret is not null)
        {
            form["client_secret"] = clientSecret;
        }

        foreach (var (key, value) in oauth.TokenRequest.Headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        foreach (var (key, value) in oauth.TokenRequest.Body)
        {
            form[key] = value;
        }

        request.Content = new FormUrlEncodedContent(form);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var preferred = string.IsNullOrEmpty(oauth.TokenField) ? "access_token" : oauth.TokenField;
            var other = preferred == "id_token" ? "access_token" : "id_token";
            string? ReadField(string name) => root.TryGetProperty(name, out var el) ? el.GetString() : null;
            var accessToken = ReadField(preferred) ?? ReadField(other);
            if (accessToken is null)
            {
                return null;
            }

            var refreshToken = ReadField("refresh_token");
            var idToken = ReadField("id_token");
            var expiresIn = root.TryGetProperty("expires_in", out var eiEl) && eiEl.TryGetInt32(out var ei)
                ? ei
                : 3600;
            return (accessToken, refreshToken, expiresIn, idToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<Dictionary<string, JsonElement>> ReadTokenCacheAsync(string path, CancellationToken ct)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.Clone();
            }
        }
        catch (JsonException)
        {
            // empty
        }

        return result;
    }

    private static async Task WriteTokenCacheAsync(
        string path, Dictionary<string, JsonElement> cache, CancellationToken ct)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in cache)
        {
            obj[key] = JsonNode.Parse(value.GetRawText());
        }

        var tmpPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(tmpPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct)
            .ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(tmpPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(tmpPath, path, overwrite: true);
    }

    private static void StoreTokenInCache(
        Dictionary<string, JsonElement> cache,
        string profileName,
        (string AccessToken, string? RefreshToken, int ExpiresIn, string? IdToken) token)
    {
        var obj = new JsonObject
        {
            ["access_token"] = token.AccessToken,
            ["expires_at"] = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn).ToString("o"),
        };
        if (token.RefreshToken is not null)
        {
            obj["refresh_token"] = token.RefreshToken;
        }

        if (token.IdToken is not null)
        {
            obj["id_token"] = token.IdToken;
        }

        cache[profileName] = JsonDocument.Parse(obj.ToJsonString()).RootElement.Clone();
    }

    private static async Task<T> WithTokenCacheLockAsync<T>(string cachePath, Func<Task<T>> action)
    {
        var lockPath = cachePath + ".lock";
        FileStream? lockStream = null;
        for (var attempt = 0; attempt < 100 && lockStream is null; attempt++)
        {
            try
            {
                lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        if (lockStream is null)
        {
            throw new InvalidOperationException(
                "Could not acquire the token cache lock (.auth-cache.json.lock) after multiple attempts.");
        }

        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            await lockStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
