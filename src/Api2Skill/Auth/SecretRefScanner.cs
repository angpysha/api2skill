using System.Text.RegularExpressions;

namespace Api2Skill.Auth;

/// <summary>
/// Collects every distinct <c>{secret:NAME}</c> reference across an <see cref="AuthConfig"/>'s
/// profiles (FR-007), so the generator can scaffold matching placeholder keys into
/// <c>secrets.example.json</c> (FR-008) without ever reading a real secret.
/// </summary>
public static partial class SecretRefScanner
{
    [GeneratedRegex(@"\{secret:([^}]+)\}")]
    private static partial Regex SecretRefPattern();

    public static IReadOnlyList<string> Scan(AuthConfig config)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var profile in config.Profiles)
        {
            foreach (var value in ValuesFor(profile))
            {
                if (value is null)
                {
                    continue;
                }
                foreach (Match m in SecretRefPattern().Matches(value))
                {
                    names.Add(m.Groups[1].Value);
                }
            }
        }
        return [.. names];
    }

    private static IEnumerable<string?> ValuesFor(AuthProfile profile)
    {
        switch (profile.Type)
        {
            case AuthType.Bearer:
                yield return profile.Bearer!.Token;
                break;
            case AuthType.Basic:
                yield return profile.Basic!.Username;
                yield return profile.Basic!.Password;
                break;
            case AuthType.Custom:
                foreach (var h in profile.Custom!.Headers)
                {
                    yield return h.Value;
                }
                break;
            case AuthType.OAuth2:
                yield return profile.OAuth!.ClientId;
                yield return profile.OAuth!.ClientSecret;
                break;
            case AuthType.Script:
                // The command string is executed locally, not treated as a secret-ref host.
                break;
        }
    }
}
