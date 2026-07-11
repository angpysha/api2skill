namespace Api2Skill.Auth;

/// <summary>
/// Two attached profiles would set the same header on one operation — an unresolvable
/// generation-time error (FR-021a), not a warning.
/// </summary>
public sealed class AuthConfigCollisionException(string message) : Exception(message);

/// <summary>
/// Resolves, for every operation, the ordered list of applicable auth-profile names — global
/// profiles union any profile attached to a tag the operation carries (FR-004/005) — enforces
/// the header-collision invariant (FR-021a), and warns about tag attachments that match no
/// operation (FR-021). An operation with zero applicable profiles keeps its spec-derived
/// behavior (FR-006) — callers treat an absent entry that way.
/// </summary>
public static class AttachmentResolver
{
    public sealed record Result(
        IReadOnlyDictionary<string, IReadOnlyList<string>> ProfileNamesByOperationId,
        IReadOnlyList<string> Warnings);

    public static Result Resolve(AuthConfig config, IReadOnlyDictionary<string, IReadOnlyList<string>> operationTags)
    {
        var warnings = new List<string>();
        var byOperation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var globalProfiles = config.Profiles.Where(p => p.Attach.Scope == AttachScope.Global).ToList();
        var tagProfiles = config.Profiles.Where(p => p.Attach.Scope == AttachScope.Tags).ToList();

        var allOperationTagValues = operationTags.Values.SelectMany(t => t).ToHashSet(StringComparer.Ordinal);
        foreach (var profile in tagProfiles)
        {
            foreach (var tag in profile.Attach.Tags)
            {
                if (!allOperationTagValues.Contains(tag))
                {
                    warnings.Add($"Auth profile '{profile.Name}' is attached to tag '{tag}', which matches no operation.");
                }
            }
        }

        foreach (var (operationId, tags) in operationTags)
        {
            var applicable = new List<AuthProfile>(globalProfiles);
            applicable.AddRange(tagProfiles.Where(p => p.Attach.Tags.Any(t => tags.Contains(t, StringComparer.Ordinal))));

            if (applicable.Count == 0)
            {
                continue;
            }

            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in applicable)
            {
                foreach (var header in HeaderNamesFor(profile))
                {
                    if (seen.TryGetValue(header, out var owner))
                    {
                        throw new AuthConfigCollisionException(
                            $"Auth profiles '{owner}' and '{profile.Name}' both set header '{header}' for operation '{operationId}'.");
                    }
                    seen[header] = profile.Name;
                }
            }

            byOperation[operationId] = [.. applicable.Select(p => p.Name)];
        }

        return new Result(byOperation, warnings);
    }

    private static IEnumerable<string> HeaderNamesFor(AuthProfile profile) => profile.Type switch
    {
        AuthType.Bearer => ["Authorization"],
        AuthType.Basic => ["Authorization"],
        AuthType.OAuth2 => ["Authorization"],
        AuthType.Script => [profile.Script!.Header],
        AuthType.Custom => profile.Custom!.Headers.Select(h => h.Name),
        _ => [],
    };
}
