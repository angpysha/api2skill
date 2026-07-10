namespace Api2Skill.Model;

/// <summary>
/// Which of the four supported auth mechanisms (spec D4) a scheme maps to.
/// <see cref="Unsupported"/> covers any OpenAPI security scheme api2skill doesn't generate
/// working auth for (e.g. openIdConnect, mutualTLS, or an OAuth2 scheme with no
/// clientCredentials flow) — operations using it still generate, with a warning (EC-6).
/// </summary>
public enum SecuritySchemeKind
{
    ApiKey,
    Bearer,
    Basic,
    OAuth2,
    Unsupported,
}

public enum ApiKeyLocation
{
    Header,
    Query,
}

/// <summary>
/// One security scheme from the spec's `components.securitySchemes`, normalized to what the
/// dispatcher needs to apply it, plus the secrets.json keys it requires (data-model.md
/// "Derived: Secrets schema").
/// </summary>
public sealed record SecuritySchemeModel(
    string Id,
    SecuritySchemeKind Kind,
    string? ApiKeyName,
    ApiKeyLocation? ApiKeyLocation,
    string? OAuthTokenUrl,
    IReadOnlyList<string> OAuthScopes,
    IReadOnlyList<string> SecretKeys);
