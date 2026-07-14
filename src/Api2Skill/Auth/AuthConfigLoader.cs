using System.Text.Json;

namespace Api2Skill.Auth;

/// <summary>Malformed/invalid <c>auth.json</c> (contracts/cli.md — maps to exit code 5).</summary>
public sealed class AuthConfigException(string message) : Exception(message);

/// <summary>
/// <c>--auth &lt;type&gt;</c> named an interactive type (<c>oauth2</c>/<c>entra</c>) that the
/// shorthand cannot express (contracts/cli.md — maps to exit code 2, usage error).
/// </summary>
public sealed class AuthShorthandUnsupportedException(string message) : Exception(message);

/// <summary>
/// Parses, validates, and maps <c>auth.json</c> into the domain <see cref="AuthConfig"/>
/// (contracts/auth-config.md), and scaffolds the <c>--auth &lt;type&gt;</c> shorthand.
/// </summary>
public static class AuthConfigLoader
{
    private static readonly HashSet<string> ShorthandTypes =
        new(StringComparer.OrdinalIgnoreCase) { "bearer", "basic", "custom" };

    /// <summary>
    /// Loads and validates <c>auth.json</c> from <paramref name="path"/>. <paramref name="rawJson"/>
    /// carries the untouched file text out so the caller can copy it **verbatim** into the
    /// generated skill directory (contracts/cli.md determinism — the user's own formatting is
    /// preserved, not re-serialized).
    /// </summary>
    public static AuthConfig LoadFromFile(string path, out string rawJson)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Auth config not found: {path}", path);
        }

        try
        {
            rawJson = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new AuthConfigException($"Failed to read auth config '{path}': {ex.Message}");
        }

        return Parse(rawJson);
    }

    public static AuthConfig Parse(string json)
    {
        AuthConfigDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, AuthConfigJsonContext.Default.AuthConfigDto);
        }
        catch (JsonException ex)
        {
            throw new AuthConfigException($"auth.json is not valid JSON: {ex.Message}");
        }

        if (dto is null || dto.Profiles is not { Count: > 0 })
        {
            throw new AuthConfigException("auth.json must contain a non-empty \"profiles\" array.");
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var profiles = new List<AuthProfile>();
        foreach (var profileDto in dto.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profileDto.Name))
            {
                throw new AuthConfigException("Every auth profile must have a non-empty \"name\".");
            }
            if (!seenNames.Add(profileDto.Name))
            {
                throw new AuthConfigException($"Duplicate auth profile name: '{profileDto.Name}'.");
            }
            profiles.Add(MapProfile(profileDto));
        }

        return new AuthConfig(profiles);
    }

    /// <summary>
    /// <c>--auth &lt;type&gt;</c>: scaffolds a single global profile for the structure-free types
    /// only (FR-001). <c>oauth2</c>/<c>entra</c> require <c>--auth-config</c>.
    /// </summary>
    public static AuthConfig CreateShorthand(string type)
    {
        if (!ShorthandTypes.Contains(type))
        {
            throw new AuthShorthandUnsupportedException(
                $"--auth {type} is not supported by the shorthand — interactive/OAuth profiles need URLs/tenant " +
                "the shorthand can't express. Use --auth-config with a full auth.json instead (see contracts/auth-config.md).");
        }

        AuthProfile profile = type.ToLowerInvariant() switch
        {
            "bearer" => new AuthProfile(
                "default", AuthType.Bearer, Attachment.Global,
                Bearer: new BearerSettings("{secret:BEARER_TOKEN}"),
                Basic: null, Custom: null, Script: null, OAuth: null),
            "basic" => new AuthProfile(
                "default", AuthType.Basic, Attachment.Global,
                Bearer: null,
                Basic: new BasicSettings("{secret:USERNAME}", "{secret:PASSWORD}"),
                Custom: null, Script: null, OAuth: null),
            "custom" => new AuthProfile(
                "default", AuthType.Custom, Attachment.Global,
                Bearer: null, Basic: null,
                Custom: new CustomSettings([new HeaderEntry("Authorization", "{secret:TOKEN}")]),
                Script: null, OAuth: null),
            _ => throw new AuthShorthandUnsupportedException($"Unknown --auth type '{type}'."),
        };

        return new AuthConfig([profile]);
    }

    private static AuthProfile MapProfile(AuthProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Type) || !TryParseType(dto.Type, out var type))
        {
            throw new AuthConfigException(
                $"Auth profile '{dto.Name}' has an unknown type '{dto.Type}'. Supported: bearer, script, oauth2, basic, custom.");
        }

        if (dto.BrowserLaunch is not null && type != AuthType.OAuth2)
        {
            throw new AuthConfigException(
                $"Auth profile '{dto.Name}' sets \"browserLaunch\" but is type '{dto.Type}' — browserLaunch only applies to oauth2 profiles.");
        }

        if (dto.TokenField is not null && type != AuthType.OAuth2)
        {
            throw new AuthConfigException(
                $"Auth profile '{dto.Name}' sets \"tokenField\" but is type '{dto.Type}' — tokenField only applies to oauth2 profiles.");
        }

        var attach = MapAttach(dto.Attach, dto.Name!);

        return type switch
        {
            AuthType.Bearer => new AuthProfile(dto.Name!, type, attach, MapBearer(dto), null, null, null, null),
            AuthType.Basic => new AuthProfile(dto.Name!, type, attach, null, MapBasic(dto), null, null, null),
            AuthType.Custom => new AuthProfile(dto.Name!, type, attach, null, null, MapCustom(dto), null, null),
            AuthType.Script => new AuthProfile(dto.Name!, type, attach, null, null, null, MapScript(dto), null),
            AuthType.OAuth2 => new AuthProfile(dto.Name!, type, attach, null, null, null, null, MapOAuth(dto)),
            _ => throw new AuthConfigException($"Auth profile '{dto.Name}' has an unsupported type '{dto.Type}'."),
        };
    }

    private static bool TryParseType(string raw, out AuthType type)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "bearer": type = AuthType.Bearer; return true;
            case "basic": type = AuthType.Basic; return true;
            case "custom": type = AuthType.Custom; return true;
            case "script": type = AuthType.Script; return true;
            case "oauth2": case "oauth": type = AuthType.OAuth2; return true;
            default: type = default; return false;
        }
    }

    private static Attachment MapAttach(AttachDto? dto, string profileName)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Scope) || string.Equals(dto.Scope, "global", StringComparison.OrdinalIgnoreCase))
        {
            return Attachment.Global;
        }

        if (string.Equals(dto.Scope, "tags", StringComparison.OrdinalIgnoreCase))
        {
            if (dto.Tags is not { Count: > 0 })
            {
                throw new AuthConfigException($"Auth profile '{profileName}' has attach.scope=\"tags\" but no tags listed.");
            }
            return new Attachment(AttachScope.Tags, dto.Tags);
        }

        throw new AuthConfigException($"Auth profile '{profileName}' has an unknown attach.scope '{dto.Scope}'. Supported: global, tags.");
    }

    private static BearerSettings MapBearer(AuthProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (bearer) requires \"token\".");
        }
        return new BearerSettings(dto.Token);
    }

    private static BasicSettings MapBasic(AuthProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (basic) requires \"username\" and \"password\".");
        }
        return new BasicSettings(dto.Username, dto.Password);
    }

    private static CustomSettings MapCustom(AuthProfileDto dto)
    {
        if (dto.Headers is not { Count: > 0 })
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (custom) requires a non-empty \"headers\" list.");
        }

        var entries = new List<HeaderEntry>();
        foreach (var h in dto.Headers)
        {
            if (string.IsNullOrWhiteSpace(h.Name) || h.Value is null)
            {
                throw new AuthConfigException($"Auth profile '{dto.Name}' (custom) has a header with a missing name or value.");
            }
            entries.Add(new HeaderEntry(h.Name, h.Value));
        }
        return new CustomSettings(entries);
    }

    private static ScriptSettings MapScript(AuthProfileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Command))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (script) requires \"command\".");
        }
        return new ScriptSettings(dto.Command, dto.Header is { Length: > 0 } h ? h : "Authorization", dto.BearerPrefix ?? false);
    }

    private static OAuthSettings MapOAuth(AuthProfileDto dto)
    {
        var grant = string.IsNullOrWhiteSpace(dto.Grant)
            ? OAuthGrant.AuthorizationCode
            : dto.Grant.Trim().ToLowerInvariant() switch
            {
                "authorization_code" => OAuthGrant.AuthorizationCode,
                "client_credentials" => OAuthGrant.ClientCredentials,
                _ => throw new AuthConfigException($"Auth profile '{dto.Name}' (oauth2) has an unknown grant '{dto.Grant}'."),
            };

        if (string.IsNullOrWhiteSpace(dto.ClientId))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (oauth2) requires \"clientId\".");
        }

        if (string.Equals(dto.Preset, EntraPreset.Name, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(dto.Tenant) && string.IsNullOrWhiteSpace(dto.AuthUrl))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' uses the 'entra' preset and needs \"tenant\" (or explicit authUrl/tokenUrl).");
        }

        var (authUrl, tokenUrl, scopes) = EntraPreset.Expand(
            dto.Preset, dto.Tenant, dto.AuthUrl, dto.TokenUrl, dto.Scopes ?? []);

        if (grant == OAuthGrant.AuthorizationCode && string.IsNullOrWhiteSpace(authUrl))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (oauth2, authorization_code) requires \"authUrl\" (directly or via a preset).");
        }
        if (string.IsNullOrWhiteSpace(tokenUrl))
        {
            throw new AuthConfigException($"Auth profile '{dto.Name}' (oauth2) requires \"tokenUrl\" (directly or via a preset).");
        }

        var clientAuth = string.IsNullOrWhiteSpace(dto.ClientAuth)
            ? ClientAuthMethod.Body
            : dto.ClientAuth.Trim().ToLowerInvariant() switch
            {
                "body" => ClientAuthMethod.Body,
                "basic" => ClientAuthMethod.Basic,
                _ => throw new AuthConfigException($"Auth profile '{dto.Name}' (oauth2) has an unknown clientAuth '{dto.ClientAuth}'."),
            };

        var callbackUrl = string.IsNullOrWhiteSpace(dto.CallbackUrl) ? "http://localhost:8400/callback" : dto.CallbackUrl;

        var browserLaunch = string.IsNullOrWhiteSpace(dto.BrowserLaunch)
            ? "auto"
            : dto.BrowserLaunch.Trim().ToLowerInvariant() switch
            {
                "auto" => "auto",
                "clipboard" => "clipboard",
                _ => throw new AuthConfigException(
                    $"Auth profile '{dto.Name}' (oauth2) has an unknown browserLaunch '{dto.BrowserLaunch}'. Supported: auto, clipboard."),
            };

        var tokenField = string.IsNullOrWhiteSpace(dto.TokenField)
            ? "access_token"
            : dto.TokenField.Trim().ToLowerInvariant() switch
            {
                "access_token" => "access_token",
                "id_token" => "id_token",
                _ => throw new AuthConfigException(
                    $"Auth profile '{dto.Name}' (oauth2) has an unknown tokenField '{dto.TokenField}'. Supported: access_token, id_token."),
            };

        return new OAuthSettings(
            Grant: grant,
            Preset: dto.Preset,
            Tenant: dto.Tenant,
            AuthUrl: authUrl,
            TokenUrl: tokenUrl!,
            Scopes: scopes,
            CallbackUrl: callbackUrl,
            BrowserLaunch: browserLaunch,
            ClientAuth: clientAuth,
            ClientId: dto.ClientId,
            ClientSecret: dto.ClientSecret,
            AuthorizeRequest: MapExtras(dto.AuthorizeRequest),
            TokenRequest: MapExtras(dto.TokenRequest),
            TokenField: tokenField);
    }

    private static OAuthRequestExtras MapExtras(OAuthRequestExtrasDto? dto) =>
        dto is null
            ? OAuthRequestExtras.Empty
            : new OAuthRequestExtras(
                dto.Headers ?? new Dictionary<string, string>(),
                dto.Body ?? new Dictionary<string, string>());

    /// <summary>
    /// Canonical JSON for an <see cref="AuthConfig"/> that has no source file (the <c>--auth</c>
    /// shorthand synthesizes one) — written verbatim into the skill directory as <c>auth.json</c>.
    /// </summary>
    public static string Serialize(AuthConfig config)
    {
        var dto = new AuthConfigDto
        {
            Profiles = [.. config.Profiles.Select(ToDto)],
        };
        return JsonSerializer.Serialize(dto, AuthConfigJsonContext.Default.AuthConfigDto);
    }

    private static AuthProfileDto ToDto(AuthProfile p)
    {
        var dto = new AuthProfileDto
        {
            Name = p.Name,
            Type = p.Type switch
            {
                AuthType.Bearer => "bearer",
                AuthType.Basic => "basic",
                AuthType.Custom => "custom",
                AuthType.Script => "script",
                AuthType.OAuth2 => "oauth2",
                _ => throw new ArgumentOutOfRangeException(nameof(p)),
            },
            Attach = p.Attach.Scope == AttachScope.Global
                ? new AttachDto { Scope = "global" }
                : new AttachDto { Scope = "tags", Tags = [.. p.Attach.Tags] },
        };

        switch (p.Type)
        {
            case AuthType.Bearer:
                dto.Token = p.Bearer!.Token;
                break;
            case AuthType.Basic:
                dto.Username = p.Basic!.Username;
                dto.Password = p.Basic!.Password;
                break;
            case AuthType.Custom:
                dto.Headers = [.. p.Custom!.Headers.Select(h => new HeaderEntryDto { Name = h.Name, Value = h.Value })];
                break;
            case AuthType.Script:
                dto.Command = p.Script!.Command;
                dto.Header = p.Script.Header;
                dto.BearerPrefix = p.Script.BearerPrefix;
                break;
            case AuthType.OAuth2:
                dto.Grant = p.OAuth!.Grant == OAuthGrant.AuthorizationCode ? "authorization_code" : "client_credentials";
                dto.Preset = p.OAuth.Preset;
                dto.Tenant = p.OAuth.Tenant;
                dto.AuthUrl = p.OAuth.AuthUrl;
                dto.TokenUrl = p.OAuth.TokenUrl;
                dto.Scopes = [.. p.OAuth.Scopes];
                dto.CallbackUrl = p.OAuth.CallbackUrl;
                dto.BrowserLaunch = p.OAuth.BrowserLaunch == "auto" ? null : p.OAuth.BrowserLaunch;
                dto.ClientAuth = p.OAuth.ClientAuth == ClientAuthMethod.Basic ? "basic" : "body";
                dto.ClientId = p.OAuth.ClientId;
                dto.ClientSecret = p.OAuth.ClientSecret;
                dto.TokenField = p.OAuth.TokenField == "access_token" ? null : p.OAuth.TokenField;
                dto.AuthorizeRequest = new OAuthRequestExtrasDto
                {
                    Headers = new Dictionary<string, string>(p.OAuth.AuthorizeRequest.Headers),
                    Body = new Dictionary<string, string>(p.OAuth.AuthorizeRequest.Body),
                };
                dto.TokenRequest = new OAuthRequestExtrasDto
                {
                    Headers = new Dictionary<string, string>(p.OAuth.TokenRequest.Headers),
                    Body = new Dictionary<string, string>(p.OAuth.TokenRequest.Body),
                };
                break;
        }

        return dto;
    }
}
