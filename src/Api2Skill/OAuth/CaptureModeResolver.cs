namespace Api2Skill.OAuth;

/// <summary>
/// Infers <see cref="CaptureMode"/> from a callback URL (and optional explicit override).
/// Per data-model.md / research mode routing.
/// </summary>
public static class CaptureModeResolver
{
    public const string DefaultRelayBase = "https://oauth.api2skill.dev";

    public static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "::1" or "[::1]";

    /// <summary>
    /// Resolves the capture mode. Returns <c>null</c> when the URL is absolute but unsupported
    /// (caller should fail validation with exit 2), or when <paramref name="modeOverride"/> is unknown.
    /// </summary>
    public static CaptureMode? Resolve(Uri callbackUrl, string? modeOverride = null, string? relayBaseUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(modeOverride)
            && !modeOverride.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return ParseModeOverride(modeOverride);
        }

        var scheme = callbackUrl.Scheme;
        if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return IsLoopbackHost(callbackUrl.Host) ? CaptureMode.HttpLoopback : null;
        }

        if (scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            if (IsLoopbackHost(callbackUrl.Host))
            {
                return CaptureMode.HttpsLoopback;
            }

            var relayBase = relayBaseUrl;
            if (string.IsNullOrWhiteSpace(relayBase))
            {
                relayBase = Environment.GetEnvironmentVariable("API2SKILL_OAUTH_RELAY_BASE");
            }

            if (string.IsNullOrWhiteSpace(relayBase))
            {
                relayBase = DefaultRelayBase;
            }

            if (Uri.TryCreate(relayBase, UriKind.Absolute, out var relayUri)
                && string.Equals(callbackUrl.Host, relayUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return CaptureMode.Hosted;
            }

            return null;
        }

        // Non-http(s) absolute URI → custom scheme
        return CaptureMode.CustomScheme;
    }

    /// <summary>
    /// Parses an explicit CLI <c>--mode</c> value. Returns <c>null</c> for <c>auto</c>/empty
    /// (meaning infer) or for unknown tokens.
    /// </summary>
    public static CaptureMode? ParseModeOverride(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return raw.ToLowerInvariant() switch
        {
            "http" => CaptureMode.HttpLoopback,
            "https" => CaptureMode.HttpsLoopback,
            "scheme" => CaptureMode.CustomScheme,
            "hosted" => CaptureMode.Hosted,
            _ => null,
        };
    }

    /// <summary>
    /// True when <paramref name="raw"/> is a known <c>--mode</c> token (including <c>auto</c>).
    /// </summary>
    public static bool IsKnownModeToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Distinguish "unknown" from "auto": for non-auto, ParseModeOverride null ⇒ unknown
        var lower = raw.ToLowerInvariant();
        return lower is "http" or "https" or "scheme" or "hosted";
    }
}
