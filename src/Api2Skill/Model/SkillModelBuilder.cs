using System.Text.Json;
using System.Text.Json.Nodes;
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
        var schemasByOperationId = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

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

                var reachable = new HashSet<string>(StringComparer.Ordinal);
                CollectReachableSchemaNames(pathItem.Parameters, operation.Parameters, operation.RequestBody, operation.Responses, document, reachable);
                schemasByOperationId[operationId] = reachable;

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

        var filteredSchemaNames = bakedFiltered
            .Select(x => x.OperationId)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(id => schemasByOperationId.TryGetValue(id, out var set) ? set : [])
            .ToHashSet(StringComparer.Ordinal);
        var componentSchemas = SerializeComponentSchemas(document, filteredSchemaNames, specVersion);

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
            ComponentSchemas: componentSchemas,
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
        var baseId = !string.IsNullOrWhiteSpace(existing) ? existing! : SynthesizeOperationId(method, path);

        if (!seen.TryGetValue(baseId, out var count))
        {
            seen[baseId] = 1;
            return baseId;
        }

        count++;
        seen[baseId] = count;
        return $"{baseId}_{count}";
    }

    /// <summary>
    /// EC-3/FR-004c: stable id from HTTP method + path. Path alone is not unique — the same
    /// path may host multiple methods (e.g. GET and POST /items).
    /// </summary>
    private static string SynthesizeOperationId(HttpMethod method, string path) =>
        Sanitize($"{method.Method}_{path}");

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
            var schema = p.Schema is null ? null : ResolveForExpand(p.Schema);
            var detail = DescribeSchema(p.Schema);
            var type = schema is null
                ? "string"
                : FormatType(schema) ?? "string";
            string? example = null;
            if (schema?.Example is not null)
            {
                example = FormatJsonNode(schema.Example);
            }
            else if (schema?.Default is not null)
            {
                example = FormatJsonNode(schema.Default);
            }

            return new ParameterModel(
                Name: p.Name ?? string.Empty,
                In: loc,
                Required: p.Required,
                Type: type,
                Description: p.Description,
                Format: schema?.Format,
                EnumValues: ExtractEnumValues(schema),
                Example: example,
                Schema: detail);
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

        var (contentType, media) = PreferJsonMedia(content);
        var detail = DescribeSchema(media.Schema);
        var schemaName = detail?.SchemaName ?? TryGetComponentName(media.Schema);
        return new RequestBodyModel(
            contentType,
            body.Required,
            detail?.Summary ?? SummarizeSchema(media.Schema),
            detail,
            schemaName);
    }

    private static (string ContentType, IOpenApiMediaType Media) PreferJsonMedia(
        IDictionary<string, IOpenApiMediaType> content)
    {
        if (content.TryGetValue("application/json", out var json))
        {
            return ("application/json", json);
        }

        foreach (var (key, media) in content)
        {
            if (key.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
            {
                return (key, media);
            }
        }

        var first = content.First();
        return (first.Key, first.Value);
    }

    private static string? SummarizeSchema(IOpenApiSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        schema = ResolveForExpand(schema);
        var objectProps = CollectObjectProperties(schema);
        if (objectProps is { Count: > 0 })
        {
            return $"object {{ {string.Join(", ", objectProps.Keys)} }}";
        }

        if (schema.Type == JsonSchemaType.Array)
        {
            var items = SummarizeSchema(schema.Items);
            return items is null ? "array" : $"array<{items}>";
        }

        var type = FormatType(schema) ?? "object";
        var enums = ExtractEnumValues(schema);
        if (enums is { Count: > 0 })
        {
            return $"{type} enum {{ {string.Join(", ", enums)} }}";
        }

        return type;
    }

    /// <summary>
    /// Build property tables + JSON example for reference docs (bodies and complex params).
    /// Nested object/array fields are flattened with dotted / <c>[]</c> paths so callers see
    /// the full input/output model. Depth-capped to avoid runaway recursion on circular schemas.
    /// </summary>
    private static SchemaDetailModel? DescribeSchema(IOpenApiSchema? schema, int depth = 0, string? pathPrefix = null)
    {
        var state = new DescribeState();
        return DescribeSchemaCore(schema, state, depth, pathPrefix, isRoot: pathPrefix is null);
    }

    private sealed class DescribeState
    {
        public bool Truncated;
    }

    private static SchemaDetailModel? DescribeSchemaCore(
        IOpenApiSchema? schema,
        DescribeState state,
        int depth,
        string? pathPrefix,
        bool isRoot)
    {
        if (schema is null)
        {
            return null;
        }

        if (depth > 4)
        {
            state.Truncated = true;
            return null;
        }

        // Nested expansion only through depth 4 (path segments). Do not add properties at depth >= 4.
        if (!isRoot && depth >= 4)
        {
            state.Truncated = true;
            return null;
        }

        var schemaName = isRoot ? TryGetComponentName(schema) : null;
        var variants = isRoot ? ExtractVariants(schema) : null;
        var working = ResolveForExpand(schema);
        var summary = SummarizeSchema(working);
        var enumValues = ExtractEnumValues(working);
        var properties = new List<SchemaPropertyModel>();

        var objectProps = CollectObjectProperties(working);
        if (objectProps is { Count: > 0 })
        {
            var required = CollectRequired(working);
            foreach (var (name, prop) in objectProps)
            {
                if (prop is null)
                {
                    continue;
                }

                var unwrapped = ResolveForExpand(prop);
                var fieldPath = string.IsNullOrEmpty(pathPrefix) ? name : $"{pathPrefix}.{name}";
                properties.Add(new SchemaPropertyModel(
                    Name: fieldPath,
                    Type: SummarizeSchema(unwrapped) ?? FormatType(unwrapped) ?? "object",
                    Required: required.Contains(name),
                    Description: unwrapped.Description,
                    Format: unwrapped.Format,
                    EnumValues: ExtractEnumValues(unwrapped)));

                AppendNestedProperties(properties, unwrapped, fieldPath, depth + 1, state);
            }
        }
        else if (working.Type == JsonSchemaType.Array && working.Items is not null)
        {
            var items = ResolveForExpand(working.Items);
            var itemsPath = string.IsNullOrEmpty(pathPrefix) ? "items" : $"{pathPrefix}[]";
            properties.Add(new SchemaPropertyModel(
                Name: itemsPath,
                Type: SummarizeSchema(items) ?? FormatType(items) ?? "object",
                Required: false,
                Description: items.Description,
                Format: items.Format,
                EnumValues: ExtractEnumValues(items)));

            AppendNestedProperties(properties, items, itemsPath, depth + 1, state);
        }

        // Only synthesize an example at the root of a body/param schema (not nested walks).
        var example = isRoot ? PrettyPrintJson(BuildExampleJson(working, 0)) : null;
        if (properties.Count == 0
            && string.IsNullOrWhiteSpace(example)
            && string.IsNullOrWhiteSpace(summary)
            && enumValues is not { Count: > 0 }
            && schemaName is null
            && variants is not { Count: > 0 })
        {
            return null;
        }

        return new SchemaDetailModel(
            summary,
            properties,
            example,
            enumValues,
            schemaName,
            isRoot && state.Truncated,
            variants);
    }

    private static void AppendNestedProperties(
        List<SchemaPropertyModel> properties,
        IOpenApiSchema schema,
        string fieldPath,
        int depth,
        DescribeState state)
    {
        if (depth >= 4)
        {
            state.Truncated = true;
            return;
        }

        var objectProps = CollectObjectProperties(schema);
        if (objectProps is { Count: > 0 })
        {
            var nested = DescribeSchemaCore(schema, state, depth, fieldPath, isRoot: false);
            if (nested is { Properties.Count: > 0 })
            {
                foreach (var child in nested.Properties)
                {
                    if (!string.Equals(child.Name, fieldPath, StringComparison.Ordinal))
                    {
                        properties.Add(child);
                    }
                }
            }

            return;
        }

        if (schema.Type == JsonSchemaType.Array && schema.Items is not null)
        {
            var items = ResolveForExpand(schema.Items);
            var itemsPath = $"{fieldPath}[]";
            if (properties.All(p => !string.Equals(p.Name, itemsPath, StringComparison.Ordinal)))
            {
                properties.Add(new SchemaPropertyModel(
                    Name: itemsPath,
                    Type: SummarizeSchema(items) ?? FormatType(items) ?? "object",
                    Required: false,
                    Description: items.Description,
                    Format: items.Format,
                    EnumValues: ExtractEnumValues(items)));
            }

            AppendNestedProperties(properties, items, itemsPath, depth + 1, state);
        }
    }

    private static IReadOnlyList<string>? ExtractEnumValues(IOpenApiSchema? schema)
    {
        if (schema?.Enum is not { Count: > 0 } values)
        {
            return null;
        }

        return [.. values.Select(FormatJsonNode)];
    }

    private static string FormatJsonNode(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var s))
            {
                return s;
            }

            if (value.TryGetValue<bool>(out var b))
            {
                return b ? "true" : "false";
            }

            if (value.TryGetValue<long>(out var l))
            {
                return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<double>(out var d))
            {
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return node.ToJsonString();
    }

    private static string? TryGetComponentName(IOpenApiSchema? schema) =>
        schema is OpenApiSchemaReference { Reference.Id: { Length: > 0 } id } ? id : null;

    private static IReadOnlyList<SchemaVariantModel>? ExtractVariants(IOpenApiSchema schema)
    {
        IList<IOpenApiSchema>? group = null;
        if (schema.OneOf is { Count: > 0 } oneOf)
        {
            group = oneOf;
        }
        else if (schema.AnyOf is { Count: > 0 } anyOf)
        {
            group = anyOf;
        }

        if (group is null)
        {
            return null;
        }

        var variants = new List<SchemaVariantModel>();
        for (var i = 0; i < group.Count; i++)
        {
            var branch = group[i];
            if (branch is null)
            {
                continue;
            }

            variants.Add(new SchemaVariantModel(
                i,
                SummarizeSchema(branch) ?? FormatType(ResolveForExpand(branch)) ?? "object",
                TryGetComponentName(branch)));
        }

        return variants.Count > 0 ? variants : null;
    }

    /// <summary>
    /// Resolve refs and pick a single shape for MD expansion: merge <c>allOf</c> properties;
    /// for <c>oneOf</c>/<c>anyOf</c> use the first branch (variants listed separately).
    /// </summary>
    private static IOpenApiSchema ResolveForExpand(IOpenApiSchema schema)
    {
        // Prefer the first oneOf / anyOf branch that carries usable shape (grill: first for example).
        foreach (var group in new[] { schema.OneOf, schema.AnyOf })
        {
            if (group is not { Count: > 0 })
            {
                continue;
            }

            foreach (var candidate in group)
            {
                if (candidate is null)
                {
                    continue;
                }

                var inner = ResolveForExpand(candidate);
                if (inner.Properties is { Count: > 0 } || inner.Type is not null || inner.Items is not null
                    || inner.AllOf is { Count: > 0 })
                {
                    return inner;
                }
            }
        }

        // allOf: OpenApiSchemaReference already proxies; when properties live only in allOf
        // members, CollectObjectProperties merges them — return schema as-is.
        return schema;
    }

    private static Dictionary<string, IOpenApiSchema>? CollectObjectProperties(IOpenApiSchema schema)
    {
        if (schema.Properties is { Count: > 0 } direct)
        {
            var copy = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            foreach (var (k, v) in direct)
            {
                if (v is not null)
                {
                    copy[k] = v;
                }
            }

            if (schema.AllOf is { Count: > 0 } allOf)
            {
                MergeAllOfProperties(copy, allOf);
            }

            return copy.Count > 0 ? copy : null;
        }

        if (schema.AllOf is { Count: > 0 } onlyAllOf)
        {
            var merged = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            MergeAllOfProperties(merged, onlyAllOf);
            return merged.Count > 0 ? merged : null;
        }

        if (schema.Type == JsonSchemaType.Object)
        {
            return null;
        }

        return null;
    }

    private static void MergeAllOfProperties(Dictionary<string, IOpenApiSchema> target, IList<IOpenApiSchema> allOf)
    {
        foreach (var part in allOf)
        {
            if (part is null)
            {
                continue;
            }

            var resolved = ResolveForExpand(part);
            var nested = CollectObjectProperties(resolved);
            if (nested is null)
            {
                continue;
            }

            foreach (var (k, v) in nested)
            {
                target[k] = v;
            }
        }
    }

    private static HashSet<string> CollectRequired(IOpenApiSchema schema)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        if (schema.Required is { Count: > 0 } direct)
        {
            foreach (var r in direct)
            {
                required.Add(r);
            }
        }

        if (schema.AllOf is { Count: > 0 } allOf)
        {
            foreach (var part in allOf)
            {
                if (part is null)
                {
                    continue;
                }

                foreach (var r in CollectRequired(ResolveForExpand(part)))
                {
                    required.Add(r);
                }
            }
        }

        return required;
    }

    private static string? FormatType(IOpenApiSchema schema)
    {
        if (schema.Type is null)
        {
            if (schema.Properties is { Count: > 0 } || schema.AllOf is { Count: > 0 })
            {
                return "object";
            }

            if (schema.Items is not null)
            {
                return "array";
            }

            return null;
        }

        // JsonSchemaType can be a flags combo (e.g. String | Null).
        return schema.Type.Value.ToString().Replace(", ", "|", StringComparison.Ordinal);
    }

    private static string? PrettyPrintJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        try
        {
            var node = JsonNode.Parse(json);
            return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return json;
        }
    }

    private static string? BuildExampleJson(IOpenApiSchema schema, int depth)
    {
        try
        {
            return SerializeExample(schema, depth);
        }
        catch
        {
            return null;
        }
    }

    private static string? SerializeExample(IOpenApiSchema schema, int depth)
    {
        if (depth > 4)
        {
            return "null";
        }

        schema = ResolveForExpand(schema);

        if (schema.Example is not null)
        {
            return schema.Example.ToJsonString();
        }

        if (schema.Default is not null)
        {
            return schema.Default.ToJsonString();
        }

        if (schema.Enum is { Count: > 0 } enumValues)
        {
            // Prefer the first declared enum as the schematic example value.
            return enumValues[0]?.ToJsonString() ?? "null";
        }

        var objectProps = CollectObjectProperties(schema);
        if (objectProps is { Count: > 0 } || schema.Type == JsonSchemaType.Object)
        {
            if (objectProps is not { Count: > 0 })
            {
                return "{}";
            }

            var parts = new List<string>();
            foreach (var (name, prop) in objectProps)
            {
                if (prop is null)
                {
                    continue;
                }

                var value = SerializeExample(prop, depth + 1) ?? "null";
                parts.Add($"{JsonSerializer.Serialize(name)}: {value}");
            }

            return "{ " + string.Join(", ", parts) + " }";
        }

        if (schema.Type == JsonSchemaType.Array)
        {
            if (schema.Items is null)
            {
                return "[]";
            }

            var item = SerializeExample(schema.Items, depth + 1) ?? "null";
            return "[ " + item + " ]";
        }

        if (schema.Type is null)
        {
            return "null";
        }

        var t = schema.Type.Value;
        if (t.HasFlag(JsonSchemaType.Boolean))
        {
            return "false";
        }

        if (t.HasFlag(JsonSchemaType.Integer) || t.HasFlag(JsonSchemaType.Number))
        {
            return "0";
        }

        if (t.HasFlag(JsonSchemaType.String))
        {
            return schema.Format switch
            {
                "date" => "\"2026-01-01\"",
                "date-time" => "\"2026-01-01T00:00:00Z\"",
                "uuid" => "\"00000000-0000-0000-0000-000000000000\"",
                _ => "\"string\"",
            };
        }

        return "null";
    }

    private static List<ResponseModel> MapResponses(OpenApiResponses? responses)
    {
        if (responses is null)
        {
            return [];
        }

        return [.. responses.Select(kvp =>
        {
            string? contentType = null;
            string? summary = null;
            SchemaDetailModel? detail = null;
            string? schemaName = null;
            if (kvp.Value.Content is { Count: > 0 } content)
            {
                var (ct, media) = PreferJsonMedia(content);
                contentType = ct;
                detail = DescribeSchema(media.Schema);
                summary = detail?.Summary ?? SummarizeSchema(media.Schema);
                schemaName = detail?.SchemaName ?? TryGetComponentName(media.Schema);
            }

            return new ResponseModel(kvp.Key, kvp.Value.Description, contentType, summary, detail, schemaName);
        })];
    }

    private static void CollectReachableSchemaNames(
        IList<IOpenApiParameter>? pathItemParameters,
        IList<IOpenApiParameter>? operationParameters,
        IOpenApiRequestBody? requestBody,
        OpenApiResponses? responses,
        OpenApiDocument document,
        HashSet<string> sink)
    {
        foreach (var p in pathItemParameters ?? [])
        {
            NoteSchemaGraph(p.Schema, document, sink, null);
        }

        foreach (var p in operationParameters ?? [])
        {
            NoteSchemaGraph(p.Schema, document, sink, null);
        }

        if (requestBody?.Content is { Count: > 0 } bodyContent)
        {
            var (_, media) = PreferJsonMedia(bodyContent);
            NoteSchemaGraph(media.Schema, document, sink, null);
        }

        if (responses is null)
        {
            return;
        }

        foreach (var response in responses.Values)
        {
            if (response.Content is not { Count: > 0 } content)
            {
                continue;
            }

            var (_, media) = PreferJsonMedia(content);
            NoteSchemaGraph(media.Schema, document, sink, null);
        }
    }

    private static void NoteSchemaGraph(
        IOpenApiSchema? schema,
        OpenApiDocument document,
        HashSet<string> sink,
        HashSet<string>? visiting)
    {
        if (schema is null)
        {
            return;
        }

        visiting ??= new HashSet<string>(StringComparer.Ordinal);

        if (TryGetComponentName(schema) is { Length: > 0 } name)
        {
            if (!visiting.Add(name))
            {
                return;
            }

            sink.Add(name);
            if (document.Components?.Schemas is { } schemas
                && schemas.TryGetValue(name, out var target)
                && target is not null)
            {
                NoteSchemaChildren(target, document, sink, visiting);
            }

            return;
        }

        NoteSchemaChildren(schema, document, sink, visiting);
    }

    private static void NoteSchemaChildren(
        IOpenApiSchema schema,
        OpenApiDocument document,
        HashSet<string> sink,
        HashSet<string> visiting)
    {
        if (schema.Properties is { Count: > 0 } props)
        {
            foreach (var prop in props.Values)
            {
                NoteSchemaGraph(prop, document, sink, visiting);
            }
        }

        if (schema.Items is not null)
        {
            NoteSchemaGraph(schema.Items, document, sink, visiting);
        }

        foreach (var group in new[] { schema.AllOf, schema.OneOf, schema.AnyOf })
        {
            if (group is not { Count: > 0 })
            {
                continue;
            }

            foreach (var part in group)
            {
                NoteSchemaGraph(part, document, sink, visiting);
            }
        }

        if (schema.AdditionalProperties is IOpenApiSchema additional)
        {
            NoteSchemaGraph(additional, document, sink, visiting);
        }

        if (schema.Not is not null)
        {
            NoteSchemaGraph(schema.Not, document, sink, visiting);
        }
    }

    private static IReadOnlyList<ComponentSchemaModel> SerializeComponentSchemas(
        OpenApiDocument document,
        HashSet<string> names,
        OpenApiSpecVersion specVersion)
    {
        if (names.Count == 0 || document.Components?.Schemas is not { } schemas)
        {
            return [];
        }

        var result = new List<ComponentSchemaModel>();
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!schemas.TryGetValue(name, out var schema) || schema is null)
            {
                continue;
            }

            // Sync wait is intentional: Build() is sync and SerializeAsJsonAsync is the
            // Microsoft.OpenApi surface for raw schema fidelity (research.md).
            var json = schema.SerializeAsJsonAsync(specVersion).GetAwaiter().GetResult();
            result.Add(new ComponentSchemaModel(name, json.TrimEnd() + "\n"));
        }

        return result;
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
