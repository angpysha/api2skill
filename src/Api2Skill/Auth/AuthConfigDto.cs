namespace Api2Skill.Auth;

/// <summary>
/// Raw <c>auth.json</c> shape for <see cref="System.Text.Json"/> source-generated deserialization
/// (research.md R9 — AOT/trim-safe). Every field is optional/nullable here; <see cref="AuthConfigLoader"/>
/// validates and maps this into the domain <see cref="AuthConfig"/>/<see cref="AuthProfile"/> types.
/// </summary>
public sealed class AuthConfigDto
{
    public List<AuthProfileDto>? Profiles { get; set; }
}

public sealed class AuthProfileDto
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public AttachDto? Attach { get; set; }

    // bearer
    public string? Token { get; set; }

    // basic
    public string? Username { get; set; }
    public string? Password { get; set; }

    // custom
    public List<HeaderEntryDto>? Headers { get; set; }

    // script
    public string? Command { get; set; }
    public string? Header { get; set; }
    public bool? BearerPrefix { get; set; }

    // oauth2 (flat — matches contracts/auth-config.md, not nested)
    public string? Grant { get; set; }
    public string? Preset { get; set; }
    public string? Tenant { get; set; }
    public string? AuthUrl { get; set; }
    public string? TokenUrl { get; set; }
    public List<string>? Scopes { get; set; }
    public string? CallbackUrl { get; set; }
    public string? ClientAuth { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public OAuthRequestExtrasDto? AuthorizeRequest { get; set; }
    public OAuthRequestExtrasDto? TokenRequest { get; set; }
}

public sealed class AttachDto
{
    public string? Scope { get; set; }
    public List<string>? Tags { get; set; }
}

public sealed class HeaderEntryDto
{
    public string? Name { get; set; }
    public string? Value { get; set; }
}

public sealed class OAuthRequestExtrasDto
{
    public Dictionary<string, string>? Headers { get; set; }
    public Dictionary<string, string>? Body { get; set; }
}
