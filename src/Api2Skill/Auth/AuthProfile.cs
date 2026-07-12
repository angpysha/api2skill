namespace Api2Skill.Auth;

/// <summary>Which auth mechanism a profile implements (spec FR-003).</summary>
public enum AuthType
{
    Bearer,
    Script,
    OAuth2,
    Basic,
    Custom,
}

/// <summary>Where a profile applies (spec FR-004).</summary>
public enum AttachScope
{
    Global,
    Tags,
}

/// <summary>
/// A profile's scope: every operation (<see cref="AttachScope.Global"/>, the default) or only
/// operations carrying one of <see cref="Tags"/>.
/// </summary>
public sealed record Attachment(AttachScope Scope, IReadOnlyList<string> Tags)
{
    public static readonly Attachment Global = new(AttachScope.Global, []);
}

/// <summary>One header's name and value-source (secret ref or literal) for a <c>custom</c> profile.</summary>
public sealed record HeaderEntry(string Name, string Value);

/// <summary>FR-009: token sent as <c>Authorization</c>, <c>Bearer </c> prefixed iff absent.</summary>
public sealed record BearerSettings(string Token);

/// <summary>FR-010: <c>Authorization: Basic base64(user:pass)</c>.</summary>
public sealed record BasicSettings(string Username, string Password);

/// <summary>FR-011: one or more distinct headers, each independently resolved.</summary>
public sealed record CustomSettings(IReadOnlyList<HeaderEntry> Headers);

/// <summary>FR-012: a command run fresh per call; trimmed stdout becomes the header value.</summary>
public sealed record ScriptSettings(string Command, string Header, bool BearerPrefix);

public enum OAuthGrant
{
    AuthorizationCode,
    ClientCredentials,
}

public enum ClientAuthMethod
{
    Body,
    Basic,
}

/// <summary>Extra headers/body parameters merged into an authorize or token request (Postman parity).</summary>
public sealed record OAuthRequestExtras(
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Body)
{
    public static readonly OAuthRequestExtras Empty =
        new(new Dictionary<string, string>(), new Dictionary<string, string>());
}

/// <summary>FR-013/014/015/016a: OAuth2 profile settings, post-preset-expansion (already resolved).</summary>
public sealed record OAuthSettings(
    OAuthGrant Grant,
    string? Preset,
    string? Tenant,
    string? AuthUrl,
    string TokenUrl,
    IReadOnlyList<string> Scopes,
    string CallbackUrl,
    string BrowserLaunch,
    ClientAuthMethod ClientAuth,
    string ClientId,
    string? ClientSecret,
    OAuthRequestExtras AuthorizeRequest,
    OAuthRequestExtras TokenRequest);

/// <summary>
/// One named, validated auth profile from <c>auth.json</c> (data-model.md §1). Exactly one of
/// the type-specific settings is populated, matching <see cref="Type"/>.
/// </summary>
public sealed record AuthProfile(
    string Name,
    AuthType Type,
    Attachment Attach,
    BearerSettings? Bearer,
    BasicSettings? Basic,
    CustomSettings? Custom,
    ScriptSettings? Script,
    OAuthSettings? OAuth);
