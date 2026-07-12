namespace Api2Skill.Auth;

/// <summary>
/// Expands the <c>entra</c> OAuth preset (research.md R5, FR-015): a tenant identifier fills the
/// Entra/Azure AD v2.0 authorize + token endpoints and ensures <c>offline_access</c> is
/// requested (so a refresh token comes back). Any explicit <c>authUrl</c>/<c>tokenUrl</c>/
/// <c>scopes</c> on the profile override the expansion.
/// </summary>
public static class EntraPreset
{
    public const string Name = "entra";

    public static (string? AuthUrl, string? TokenUrl, IReadOnlyList<string> Scopes) Expand(
        string? preset,
        string? tenant,
        string? explicitAuthUrl,
        string? explicitTokenUrl,
        IReadOnlyList<string> explicitScopes)
    {
        if (!string.Equals(preset, Name, StringComparison.OrdinalIgnoreCase))
        {
            return (explicitAuthUrl, explicitTokenUrl, explicitScopes);
        }

        var authUrl = explicitAuthUrl is { Length: > 0 }
            ? explicitAuthUrl
            : tenant is { Length: > 0 } ? $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize" : null;
        var tokenUrl = explicitTokenUrl is { Length: > 0 }
            ? explicitTokenUrl
            : tenant is { Length: > 0 } ? $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token" : null;

        var scopes = explicitScopes.Contains("offline_access", StringComparer.Ordinal)
            ? explicitScopes
            : [.. explicitScopes, "offline_access"];

        return (authUrl, tokenUrl, scopes);
    }
}
