using System.Globalization;
using System.Text;

namespace Api2Skill.Model;

/// <summary>
/// Thrown when composition cannot produce a single skill (base URL mismatch, operationId
/// collision in fail mode, scriptKind mismatch, etc.). Callers map this to exit code 2.
/// </summary>
public sealed class CompositionException : Exception
{
    public CompositionException(string message) : base(message)
    {
    }
}

/// <summary>
/// Merges per-part <see cref="SkillModel"/> instances into one model before emit (research R1).
/// Applies D2–D4 collision rules and schema file namespacing. Per-part acquire/parse/build
/// SHOULD run in parallel (<c>Task.WhenAll</c>); compose itself is serial and fail-fast (R13).
/// </summary>
public static class SkillModelComposer
{
    /// <summary>
    /// Validates script kinds for merge (I1 / DD4). Canonical = existing target when forcing
    /// into an existing composite; otherwise all sources must match each other.
    /// </summary>
    public static void ValidateScriptKinds(
        string? canonicalScriptKind,
        IReadOnlyList<(string SpecId, string ScriptKind)> sources)
    {
        if (sources.Count == 0)
        {
            return;
        }

        var expected = canonicalScriptKind ?? sources[0].ScriptKind;
        var mismatches = sources
            .Where(s => !string.Equals(s.ScriptKind, expected, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (mismatches.Count == 0)
        {
            return;
        }

        var detail = string.Join("; ", mismatches.Select(m => $"{m.SpecId}={m.ScriptKind}"));
        throw new CompositionException(
            $"scriptKind mismatch: expected '{expected}' (canonical), but found: {detail}. " +
            "All source skills must use the same script kind.");
    }

    /// <summary>
    /// Composes ordered parts into one <see cref="SkillModel"/>. Part order is preserved in
    /// <see cref="SkillModel.SpecParts"/> and warnings are merged in that order.
    /// </summary>
    public static SkillModel Compose(
        string skillName,
        IReadOnlyList<(SpecPartManifest Manifest, SkillModel Model)> parts,
        CompositionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new CompositionException("Composition requires at least one spec part.");
        }

        options ??= new CompositionOptions();
        if (parts.Count == 1)
        {
            var (manifest, only) = parts[0];
            var singleOps = CountDistinctOperations(only);
            return only with
            {
                Name = skillName,
                BaseUrl = options.BaseUrlOverride ?? only.BaseUrl,
                SpecParts =
                [
                    new SpecPartSummary(
                        manifest.Id,
                        manifest.DisplayLabel,
                        manifest.Source,
                        singleOps),
                ],
            };
        }

        var resolvedBases = parts
            .Select(p => (
                p.Manifest.Id,
                Url: NormalizeBaseUrl(options.BaseUrlOverride ?? p.Model.BaseUrl)))
            .ToList();

        var distinctBases = resolvedBases
            .Select(x => x.Url)
            .Where(u => u is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (options.BaseUrlOverride is null && distinctBases.Count > 1)
        {
            var summary = string.Join(
                "; ",
                resolvedBases.Select(b => $"{b.Id}={b.Url ?? "(none)"}"));
            throw new CompositionException(
                "Base URL mismatch across spec parts (D2). Supply --base-url to override, or use specs that share one API root. " +
                $"Per-part URLs: {summary}");
        }

        var emptyBases = resolvedBases.Where(b => b.Url is null).ToList();
        if (options.BaseUrlOverride is null && emptyBases.Count > 0 && distinctBases.Count >= 1)
        {
            var summary = string.Join(
                "; ",
                resolvedBases.Select(b => $"{b.Id}={b.Url ?? "(none)"}"));
            throw new CompositionException(
                "Base URL mismatch across spec parts (D2). Some parts have no servers URL. " +
                $"Per-part URLs: {summary}");
        }

        var canonicalBase = options.BaseUrlOverride
            ?? distinctBases.FirstOrDefault()
            ?? resolvedBases[0].Url;

        // Collect operations with provenance; detect operationId collisions.
        var opsById = new Dictionary<string, (string SpecId, OperationModel Op, string Tag, string? TagSummary)>(
            StringComparer.Ordinal);
        var collisions = new List<(string SpecId, string OperationId)>();
        var tagged = new List<(string SpecId, string Tag, string? TagSummary, OperationModel Op)>();

        foreach (var (manifest, model) in parts)
        {
            foreach (var tag in model.Tags)
            {
                foreach (var op in tag.Operations)
                {
                    tagged.Add((manifest.Id, tag.Tag, tag.Summary, op));
                    if (opsById.TryGetValue(op.OperationId, out var existing))
                    {
                        collisions.Add((existing.SpecId, op.OperationId));
                        collisions.Add((manifest.Id, op.OperationId));
                    }
                    else
                    {
                        opsById[op.OperationId] = (manifest.Id, op, tag.Tag, tag.Summary);
                    }
                }
            }
        }

        if (collisions.Count > 0 && options.OnOperationIdConflict == OperationIdConflictMode.Fail)
        {
            var pairs = string.Join(
                ", ",
                collisions
                    .Distinct()
                    .Select(c => $"({c.SpecId}, {c.OperationId})"));
            throw new CompositionException(
                $"operationId collision across spec parts (D3). Use --on-conflict prefix to rename. Collisions: {pairs}");
        }

        // Security schemes: union by id; rename collisions to {specId}_{schemeId}.
        var schemes = new List<SecuritySchemeModel>();
        var schemeIdMap = new Dictionary<(string SpecId, string OldId), string>();
        var claimedSchemeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (manifest, model) in parts)
        {
            foreach (var scheme in model.SecuritySchemes)
            {
                var newId = scheme.Id;
                if (claimedSchemeIds.Contains(scheme.Id))
                {
                    newId = $"{manifest.Id}_{scheme.Id}";
                    // Ensure uniqueness if even the prefixed id collides.
                    var candidate = newId;
                    var n = 2;
                    while (claimedSchemeIds.Contains(candidate))
                    {
                        candidate = $"{newId}_{n.ToString(CultureInfo.InvariantCulture)}";
                        n++;
                    }

                    newId = candidate;
                }

                claimedSchemeIds.Add(newId);
                schemeIdMap[(manifest.Id, scheme.Id)] = newId;
                schemes.Add(scheme with
                {
                    Id = newId,
                    SourceSpecId = manifest.Id,
                    SecretKeys = RemapSecretKeys(scheme.SecretKeys, scheme.Id, newId),
                });
            }
        }

        // Component schemas: namespace file names on collision → {specId}.{schemaName}.
        var schemas = new List<ComponentSchemaModel>();
        var claimedSchemaNames = new HashSet<string>(StringComparer.Ordinal);
        var schemaRenameWarnings = new List<string>();

        foreach (var (manifest, model) in parts)
        {
            foreach (var schema in model.ComponentSchemas ?? [])
            {
                var name = schema.Name;
                if (claimedSchemaNames.Contains(name))
                {
                    var namespaced = $"{manifest.Id}.{schema.Name}";
                    schemaRenameWarnings.Add(
                        $"Schema '{schema.Name}' collided; writing as '{namespaced}.json' (part '{manifest.Id}').");
                    name = namespaced;
                }

                claimedSchemaNames.Add(name);
                schemas.Add(schema with { Name = name });
            }
        }

        // Tag names appearing in >1 part → prefix heading with [specId] (D5).
        var tagPartCounts = tagged
            .Select(t => (t.Tag, t.SpecId))
            .Distinct()
            .GroupBy(t => t.Tag, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SpecId).Distinct().Count(), StringComparer.Ordinal);

        var composedOps = new List<(string DisplayTag, string? TagSummary, OperationModel Op)>();
        var prefixWarnings = new List<string>();

        foreach (var (specId, tag, tagSummary, op) in tagged)
        {
            var operationId = op.OperationId;
            if (options.OnOperationIdConflict == OperationIdConflictMode.Prefix
                && collisions.Any(c => c.OperationId == op.OperationId))
            {
                // Only rename ops that actually collide.
                var collidingIds = collisions
                    .Where(c => c.OperationId == op.OperationId)
                    .Select(c => c.SpecId)
                    .Distinct(StringComparer.Ordinal)
                    .ToHashSet(StringComparer.Ordinal);
                if (collidingIds.Count > 1)
                {
                    operationId = $"{specId}__{op.OperationId}";
                    prefixWarnings.Add(
                        $"Renamed operationId '{op.OperationId}' → '{operationId}' (part '{specId}').");
                }
            }

            var securityIds = op.SecuritySchemeIds
                .Select(oldId => schemeIdMap.TryGetValue((specId, oldId), out var mapped) ? mapped : oldId)
                .ToList();

            var rewritten = op with
            {
                OperationId = operationId,
                SecuritySchemeIds = securityIds,
                SourceSpecId = specId,
            };

            var displayTag = tagPartCounts.GetValueOrDefault(tag, 1) > 1
                ? $"[{specId}] {tag}"
                : tag;
            composedOps.Add((displayTag, tagSummary, rewritten));
        }

        var tagGroups = composedOps
            .GroupBy(x => x.DisplayTag, StringComparer.Ordinal)
            .Select(g =>
            {
                var ops = g
                    .GroupBy(x => x.Op.OperationId, StringComparer.Ordinal)
                    .Select(og => og.First().Op)
                    .OrderBy(o => o.OperationId, StringComparer.Ordinal)
                    .ToList();
                return new TagGroup(g.Key, g.First().TagSummary, ops);
            })
            .OrderBy(t => t.Tag, StringComparer.Ordinal)
            .ToList();

        var warnings = new List<string>();
        foreach (var (_, model) in parts)
        {
            warnings.AddRange(model.Warnings);
        }

        warnings.AddRange(prefixWarnings.Distinct(StringComparer.Ordinal));
        warnings.AddRange(schemaRenameWarnings);

        var first = parts[0].Model;
        var title = parts.Count == 1
            ? first.Title
            : string.Join(" + ", parts.Select(p => p.Model.Title).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct());

        var description = parts.Count == 1
            ? first.Description
            : "Composite skill from multiple OpenAPI documents.";

        var version = first.Version;
        var insecure = parts.Any(p => p.Model.InsecureDefault);
        // Prefer explicit auth from first part that has it (merge policy aligns with auth.json first-wins).
        var authConfig = parts.Select(p => p.Model.AuthConfig).FirstOrDefault(a => a is not null);
        var scaffold = parts.Select(p => p.Model.AuthScaffoldGuidance).FirstOrDefault(a => a is not null);

        var specParts = parts.Select(p =>
        {
            var count = CountDistinctOperations(p.Model);
            return new SpecPartSummary(
                p.Manifest.Id,
                p.Manifest.DisplayLabel,
                p.Manifest.Source,
                count);
        }).ToList();

        return new SkillModel(
            Name: skillName,
            Title: string.IsNullOrWhiteSpace(title) ? skillName : title,
            Description: description,
            Version: version,
            BaseUrl: canonicalBase,
            SpecVersion: first.SpecVersion,
            SecuritySchemes: schemes,
            Tags: tagGroups,
            Warnings: warnings,
            ComponentSchemas: schemas,
            InsecureDefault: insecure,
            AuthConfig: authConfig,
            AuthScaffoldGuidance: scaffold,
            SpecParts: specParts);
    }

    /// <summary>
    /// Normalizes a base URL for D2 comparison: lowercase scheme/host, default ports stripped,
    /// trailing slash trimmed on the path.
    /// </summary>
    public static string? NormalizeBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            // Non-absolute — compare trimmed lowercase as last resort.
            return url.Trim().TrimEnd('/').ToLowerInvariant();
        }

        var builder = new StringBuilder();
        builder.Append(uri.Scheme.ToLowerInvariant());
        builder.Append("://");
        builder.Append(uri.Host.ToLowerInvariant());

        var isDefaultPort =
            (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && uri.Port == 80)
            || (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && uri.Port == 443);
        if (!isDefaultPort && uri.Port > 0)
        {
            builder.Append(':');
            builder.Append(uri.Port.ToString(CultureInfo.InvariantCulture));
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Length > 0 && path != "/")
        {
            builder.Append(path);
        }

        return builder.ToString();
    }

    private static int CountDistinctOperations(SkillModel model) =>
        model.Tags.SelectMany(t => t.Operations).Select(o => o.OperationId).Distinct(StringComparer.Ordinal).Count();

    private static IReadOnlyList<string> RemapSecretKeys(
        IReadOnlyList<string> keys,
        string oldSchemeId,
        string newSchemeId)
    {
        if (string.Equals(oldSchemeId, newSchemeId, StringComparison.Ordinal) || keys.Count == 0)
        {
            return keys;
        }

        // Secret keys are typically "{schemeId}.{field}" — rewrite prefix when scheme was renamed.
        var prefix = oldSchemeId + ".";
        return keys
            .Select(k => k.StartsWith(prefix, StringComparison.Ordinal)
                ? newSchemeId + k[oldSchemeId.Length..]
                : k)
            .ToList();
    }
}
