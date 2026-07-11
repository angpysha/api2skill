using Api2Skill.Auth;
using Microsoft.OpenApi;
// Both Microsoft.OpenApi and this namespace declare a `ParameterLocation` enum; alias the
// OpenAPI one explicitly everywhere it's read from the parsed document so bare
// `ParameterLocation` unambiguously means this project's own Model type (same-namespace
// resolution already prefers it, but the alias makes every call site self-documenting).
using OaParameterLocation = Microsoft.OpenApi.ParameterLocation;

namespace Api2Skill.Model;

/// <summary>Generator options that affect model shape (FR-004b, FR-008).</summary>
public sealed record BuildOptions(
    string Name,
    IReadOnlyList<string>? IncludeSelectors = null,
    IReadOnlyList<string>? ExcludeSelectors = null,
    string? BaseUrlOverride = null,
    bool InsecureDefault = false,
    AuthConfig? AuthConfig = null);

/// <summary>
/// Maps a parsed <see cref="OpenApiDocument"/> to the emitter-agnostic <see cref="SkillModel"/>
/// (data-model.md). This is the ONLY place that touches Microsoft.OpenApi types outside of
/// Parsing — everything downstream (writers, emitters) depends solely on the model this
/// produces (Constitution III).
/// </summary>
public static class SkillModelBuilder
{
    private const string DefaultTag = "default";

    public static SkillModel Build(OpenApiDocument document, OpenApiSpecVersion specVersion, BuildOptions options)
    {
        var warnings = new List<string>();
        var schemesById = BuildSecuritySchemeIndex(document, warnings);

        var tagged = new List<(string Tag, string OperationId, OperationModel Op)>();
        var seenIds = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var pathEntry in document.Paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;
            if (pathItem is null)
            {
                continue;
            }

            // IOpenApiPathItem.Operations is nullable in Microsoft.OpenApi even though the
            // reflection-derived docs don't always surface the `?` — an empty path item is
            // valid (e.g. only `parameters`/`servers` set, no HTTP methods).
            foreach (var opEntry in pathItem.Operations ?? [])
            {
                var httpMethod = opEntry.Key;
                var operation = opEntry.Value;
                var operationId = ResolveOperationId(operation.OperationId, httpMethod, path, seenIds);

                var tags = operation.Tags is { Count: > 0 }
                    ? operation.Tags
                        .Select(t => t.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .ToList()
                    : [];
                if (tags.Count == 0)
                {
                    tags = [DefaultTag];
                }

                var securityIds = ResolveSecurityIds(operation.Security, document.Security);

                var opModel = new OperationModel(
                    OperationId: operationId,
                    Method: httpMethod,
                    PathTemplate: path,
                    Summary: operation.Summary,
                    Description: operation.Description,
                    Parameters: MapParameters(pathItem.Parameters, operation.Parameters),
                    RequestBody: MapRequestBody(operation.RequestBody),
                    SecuritySchemeIds: securityIds,
                    Responses: MapResponses(operation.Responses));

                foreach (var tag in tags)
                {
                    tagged.Add((tag, operationId, opModel));
                }
            }
        }

        var filtered = ApplyFilters(tagged, options.IncludeSelectors, options.ExcludeSelectors);
        var bakedFiltered = ResolveExplicitAuth(filtered, options.AuthConfig, warnings);

        var tagGroups = bakedFiltered
            .GroupBy(x => x.Tag, StringComparer.Ordinal)
            .OrderBy(g => TagOrder(g.Key, document))
            .Select(g => new TagGroup(g.Key, TagSummary(g.Key, document), [.. g.Select(x => x.Op)]))
            .ToList();

        var usedSchemeIds = tagGroups
            .SelectMany(g => g.Operations)
            .SelectMany(o => o.SecuritySchemeIds)
            .ToHashSet(StringComparer.Ordinal);
        var usedSchemes = schemesById.Values
            .Where(s => usedSchemeIds.Contains(s.Id))
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .ToList();

        var baseUrl = options.BaseUrlOverride ?? document.Servers?.FirstOrDefault()?.Url;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            warnings.Add("The spec has no `servers` entry; supply --base-url or set \"baseUrl\" in secrets.json (EC-7).");
        }

        if (tagGroups.Count == 0)
        {
            warnings.Add("The spec (after any filters) has no callable operations; generated a minimal skill (OQ-4).");
        }

        if (options.InsecureDefault)
        {
            warnings.Add("--insecure was set: this skill's dispatcher accepts untrusted TLS certificates by default. Dev/local use only — regenerate without --insecure before pointing it at any non-dev target.");
        }

        return new SkillModel(
            Name: options.Name,
            Title: document.Info?.Title is { Length: > 0 } title ? title : options.Name,
            Description: document.Info?.Description,
            Version: document.Info?.Version,
            BaseUrl: baseUrl,
            SpecVersion: MapSpecVersion(specVersion),
            SecuritySchemes: usedSchemes,
            Tags: tagGroups,
            Warnings: warnings,
            InsecureDefault: options.InsecureDefault,
            AuthConfig: options.AuthConfig);
    }

    /// <summary>
    /// Runs <see cref="AttachmentResolver"/> (when an explicit <see cref="AuthConfig"/> was
    /// supplied) and bakes each operation's resolved <c>AuthProfileNames</c> onto its
    /// <see cref="OperationModel"/> (FR-006). An operation can appear under multiple tags — the
    /// same <paramref name="filtered"/> operation is resolved exactly once and every occurrence
    /// is rewritten to the identical resulting instance, so every <see cref="TagGroup"/> agrees.
    /// </summary>
    private static List<(string Tag, string OperationId, OperationModel Op)> ResolveExplicitAuth(
        List<(string Tag, string OperationId, OperationModel Op)> filtered,
        AuthConfig? authConfig,
        List<string> warnings)
    {
        if (authConfig is null)
        {
            return filtered;
        }

        var operationTags = filtered
            .GroupBy(x => x.OperationId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Tag).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var resolved = AttachmentResolver.Resolve(authConfig, operationTags);
        warnings.AddRange(resolved.Warnings);

        var bakedById = new Dictionary<string, OperationModel>(StringComparer.Ordinal);
        return [.. filtered.Select(x =>
        {
            if (!bakedById.TryGetValue(x.OperationId, out var op))
            {
                op = resolved.ProfileNamesByOperationId.TryGetValue(x.OperationId, out var names)
                    ? x.Op with { AuthProfileNames = names }
                    : x.Op;
                bakedById[x.OperationId] = op;
            }
            return (x.Tag, x.OperationId, Op: op);
        })];
    }

    private static SpecVersionKind MapSpecVersion(OpenApiSpecVersion version) => version switch
    {
        OpenApiSpecVersion.OpenApi2_0 => SpecVersionKind.OpenApi2_0,
        OpenApiSpecVersion.OpenApi3_0 => SpecVersionKind.OpenApi3_0,
        OpenApiSpecVersion.OpenApi3_1 => SpecVersionKind.OpenApi3_1,
        _ => SpecVersionKind.OpenApi3_2,
    };

    private static Dictionary<string, SecuritySchemeModel> BuildSecuritySchemeIndex(
        OpenApiDocument document, List<string> warnings)
    {
        var result = new Dictionary<string, SecuritySchemeModel>(StringComparer.Ordinal);
        var schemes = document.Components?.SecuritySchemes;
        if (schemes is null)
        {
            return result;
        }

        foreach (var (id, scheme) in schemes)
        {
            result[id] = MapScheme(id, scheme, warnings);
        }

        return result;
    }

    private static SecuritySchemeModel MapScheme(string id, IOpenApiSecurityScheme scheme, List<string> warnings)
    {
        switch (scheme.Type)
        {
            case SecuritySchemeType.ApiKey:
                var location = scheme.In == OaParameterLocation.Query ? ApiKeyLocation.Query : ApiKeyLocation.Header;
                return new SecuritySchemeModel(id, SecuritySchemeKind.ApiKey, scheme.Name, location, null, [], ["apiKey"]);

            case SecuritySchemeType.Http when string.Equals(scheme.Scheme, "bearer", StringComparison.OrdinalIgnoreCase):
                return new SecuritySchemeModel(id, SecuritySchemeKind.Bearer, null, null, null, [], ["bearerToken"]);

            case SecuritySchemeType.Http when string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase):
                return new SecuritySchemeModel(id, SecuritySchemeKind.Basic, null, null, null, [], ["username", "password"]);

            case SecuritySchemeType.OAuth2:
                var tokenUrl = scheme.Flows?.ClientCredentials?.TokenUrl?.ToString();
                var scopes = scheme.Flows?.ClientCredentials?.Scopes?.Keys.ToList() ?? [];
                if (tokenUrl is null)
                {
                    warnings.Add($"Security scheme '{id}' is OAuth2 but has no clientCredentials flow/tokenUrl (EC-6); treating as unsupported.");
                    return new SecuritySchemeModel(id, SecuritySchemeKind.Unsupported, null, null, null, [], []);
                }
                return new SecuritySchemeModel(id, SecuritySchemeKind.OAuth2, null, null, tokenUrl, scopes, ["clientId", "clientSecret", "tokenUrl"]);

            default:
                warnings.Add($"Security scheme '{id}' uses an unsupported type ({scheme.Type}); operations using it need manual auth (EC-6).");
                return new SecuritySchemeModel(id, SecuritySchemeKind.Unsupported, null, null, null, [], []);
        }
    }

    private static string ResolveOperationId(
        string? existing, HttpMethod method, string path, Dictionary<string, int> seen)
    {
        var baseId = !string.IsNullOrWhiteSpace(existing) ? existing! : Sanitize($"{method.Method}_{path}");

        if (!seen.TryGetValue(baseId, out var count))
        {
            seen[baseId] = 1;
            return baseId;
        }

        count++;
        seen[baseId] = count;
        return $"{baseId}_{count}";
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("__", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("__", "_");
        }
        return collapsed.Trim('_');
    }

    private static List<string> ResolveSecurityIds(
        IList<OpenApiSecurityRequirement>? operationSecurity,
        IList<OpenApiSecurityRequirement>? documentSecurity)
    {
        // Operation-level `security` REPLACES document-level entirely when present (including
        // an explicit empty list meaning "no auth") — it is never merged with the document
        // default. Only fall back to the document default when the operation doesn't specify
        // `security` at all.
        var effective = operationSecurity ?? documentSecurity;
        if (effective is null || effective.Count == 0)
        {
            return [];
        }

        var ids = new List<string>();
        foreach (var requirement in effective)
        {
            foreach (var schemeRef in requirement.Keys)
            {
                var id = schemeRef.Reference?.Id;
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id, StringComparer.Ordinal))
                {
                    ids.Add(id);
                }
            }
        }
        return ids;
    }

    private static List<ParameterModel> MapParameters(
        IList<IOpenApiParameter>? pathItemParameters, IList<IOpenApiParameter>? operationParameters)
    {
        // Path-item-level parameters apply to every operation under it; operation-level
        // parameters are added/override by (name, location) per the OpenAPI spec.
        var byKey = new Dictionary<(string Name, ParameterLocation In), IOpenApiParameter>();
        foreach (var p in pathItemParameters ?? [])
        {
            if (TryMapLocation(p.In, out var loc))
            {
                byKey[(p.Name ?? string.Empty, loc)] = p;
            }
        }
        foreach (var p in operationParameters ?? [])
        {
            if (TryMapLocation(p.In, out var loc))
            {
                byKey[(p.Name ?? string.Empty, loc)] = p;
            }
        }

        return [.. byKey.Values.Select(p =>
        {
            TryMapLocation(p.In, out var loc);
            return new ParameterModel(
                Name: p.Name ?? string.Empty,
                In: loc,
                Required: p.Required,
                Type: p.Schema?.Type?.ToString() ?? "string",
                Description: p.Description);
        })];
    }

    private static bool TryMapLocation(OaParameterLocation? source, out ParameterLocation location)
    {
        switch (source)
        {
            case OaParameterLocation.Path:
                location = ParameterLocation.Path;
                return true;
            case OaParameterLocation.Query:
                location = ParameterLocation.Query;
                return true;
            case OaParameterLocation.Header:
                location = ParameterLocation.Header;
                return true;
            default:
                // Cookie / QueryString are out of MVP scope (data-model.md) — dropped.
                location = ParameterLocation.Query;
                return false;
        }
    }

    private static RequestBodyModel? MapRequestBody(IOpenApiRequestBody? body)
    {
        if (body?.Content is not { Count: > 0 } content)
        {
            return null;
        }

        var (contentType, media) = content.TryGetValue("application/json", out var json)
            ? ("application/json", json)
            : (content.Keys.First(), content.Values.First());

        return new RequestBodyModel(contentType, body.Required, SummarizeSchema(media.Schema));
    }

    private static string? SummarizeSchema(IOpenApiSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        if (schema.Type == JsonSchemaType.Object && schema.Properties is { Count: > 0 })
        {
            return $"object {{ {string.Join(", ", schema.Properties.Keys)} }}";
        }

        return schema.Type?.ToString() ?? "object";
    }

    private static List<ResponseModel> MapResponses(OpenApiResponses? responses)
    {
        if (responses is null)
        {
            return [];
        }

        return [.. responses.Select(kvp => new ResponseModel(kvp.Key, kvp.Value.Description))];
    }

    private static List<(string Tag, string OperationId, OperationModel Op)> ApplyFilters(
        List<(string Tag, string OperationId, OperationModel Op)> all,
        IReadOnlyList<string>? include,
        IReadOnlyList<string>? exclude)
    {
        IEnumerable<(string Tag, string OperationId, OperationModel Op)> result = all;

        if (include is { Count: > 0 })
        {
            result = result.Where(x => include.Any(sel => MatchesSelector(sel, x.Tag, x.OperationId, x.Op.PathTemplate)));
        }
        if (exclude is { Count: > 0 })
        {
            result = result.Where(x => !exclude.Any(sel => MatchesSelector(sel, x.Tag, x.OperationId, x.Op.PathTemplate)));
        }

        return [.. result];
    }

    private static bool MatchesSelector(string selector, string tag, string operationId, string path)
    {
        var parts = selector.Split(':', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        var (kind, value) = (parts[0].Trim().ToLowerInvariant(), parts[1].Trim());
        return kind switch
        {
            "tag" => string.Equals(tag, value, StringComparison.OrdinalIgnoreCase),
            "op" => string.Equals(operationId, value, StringComparison.OrdinalIgnoreCase),
            "path" => IsPathGlobMatch(path, value),
            _ => false,
        };
    }

    private static bool IsPathGlobMatch(string path, string globPattern)
    {
        // Minimal glob: '*' matches any run of characters; everything else is literal.
        var regexPattern = "^" + string.Join(".*", globPattern.Split('*').Select(System.Text.RegularExpressions.Regex.Escape)) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(path, regexPattern);
    }

    private static int TagOrder(string tag, OpenApiDocument document)
    {
        if (tag == DefaultTag)
        {
            return int.MaxValue;
        }

        if (document.Tags is not { Count: > 0 } tags)
        {
            return int.MaxValue - 1;
        }

        var index = 0;
        foreach (var t in tags)
        {
            if (string.Equals(t.Name, tag, StringComparison.Ordinal))
            {
                return index;
            }
            index++;
        }
        return int.MaxValue - 1;
    }

    private static string? TagSummary(string tag, OpenApiDocument document) =>
        document.Tags?.FirstOrDefault(t => string.Equals(t.Name, tag, StringComparison.Ordinal))?.Description;
}
