namespace Api2Skill.Auth;

/// <summary>
/// Two attached profiles would set the same header on one operation — an unresolvable
/// generation-time error (FR-021a), not a warning.
/// </summary>
public sealed class AuthConfigCollisionException(string message) : Exception(message);

/// <summary>
/// Resolves, for every operation, the ordered list of applicable auth-profile names.
/// Unscoped profiles whose name matches a known security scheme id apply only to operations
/// that list that scheme (FR-005/FR-006). Truly global profiles (name matches no scheme) still
/// apply everywhere. Tag-scoped profiles continue to use <see cref="AttachScope.Tags"/>.
/// Enforces the header-collision invariant (FR-021a) and warns about unused tag attachments
/// and profiles that never attach to any operation.
/// </summary>
public static class AttachmentResolver
{
    public sealed record Result(
        IReadOnlyDictionary<string, IReadOnlyList<string>> ProfileNamesByOperationId,
        IReadOnlyList<string> Warnings);

    /// <param name="operationTags">operationId → tags carried by that operation.</param>
    /// <param name="operationSchemeIds">
    /// operationId → security scheme ids on that operation. When null/empty, scheme-named
    /// unscoped profiles fall back to true-global behavior (legacy).
    /// </param>
    /// <param name="knownSchemeIds">
    /// All scheme ids in the skill (for deciding whether a profile name is scheme-scoped).
    /// Defaults to the union of <paramref name="operationSchemeIds"/> values.
    /// </param>
    public static Result Resolve(
        AuthConfig config,
        IReadOnlyDictionary<string, IReadOnlyList<string>> operationTags,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? operationSchemeIds = null,
        IReadOnlySet<string>? knownSchemeIds = null)
    {
        var warnings = new List<string>();
        var byOperation = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var schemesByOperation = operationSchemeIds
            ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var schemeIds = knownSchemeIds
            ?? schemesByOperation.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);

        var tagProfiles = config.Profiles.Where(p => p.Attach.Scope == AttachScope.Tags).ToList();
        var unscopedProfiles = config.Profiles.Where(p => p.Attach.Scope == AttachScope.Global).ToList();

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

        var usedProfileNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (operationId, tags) in operationTags)
        {
            schemesByOperation.TryGetValue(operationId, out var opSchemes);
            opSchemes ??= [];

            var applicable = new List<AuthProfile>();
            foreach (var profile in unscopedProfiles)
            {
                if (schemeIds.Contains(profile.Name))
                {
                    // Scheme-named unscoped profile → only ops that declare that scheme.
                    if (opSchemes.Contains(profile.Name, StringComparer.Ordinal))
                    {
                        applicable.Add(profile);
                    }
                }
                else
                {
                    // Truly global (name is not a known scheme id).
                    applicable.Add(profile);
                }
            }

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
                        var schemeHint = opSchemes.Count > 0
                            ? $" (operation schemes: {string.Join(", ", opSchemes)})"
                            : string.Empty;
                        throw new AuthConfigCollisionException(
                            $"Auth profiles '{owner}' and '{profile.Name}' both set header '{header}' for operation '{operationId}'{schemeHint}.");
                    }
                    seen[header] = profile.Name;
                }
            }

            foreach (var profile in applicable)
            {
                usedProfileNames.Add(profile.Name);
            }

            byOperation[operationId] = [.. applicable.Select(p => p.Name)];
        }

        foreach (var profile in config.Profiles)
        {
            if (!usedProfileNames.Contains(profile.Name))
            {
                warnings.Add(
                    $"Auth profile '{profile.Name}' in auth.json is never referenced by any generated operation — check scheme id / attach targeting.");
            }
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
