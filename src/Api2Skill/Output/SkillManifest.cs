using System.Text.Json;
using System.Text.Json.Nodes;
using Api2Skill.Cli;
using Api2Skill.Model;

namespace Api2Skill.Output;

/// <summary>
/// The options used to produce a given generated skill, recorded so <c>update</c> can regenerate
/// it later without the caller re-supplying every original flag (spec.md FR-001). Secret-free —
/// never records resolved credentials, only the same non-secret options <c>generate</c> itself
/// was given. v2 uses <see cref="SpecSources"/>; legacy singular <see cref="SpecSource"/> still loads.
/// </summary>
public sealed record SkillManifest(
    string Name,
    string SpecSource,
    string ScriptKind,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude,
    string? Format,
    string? BaseUrl,
    bool Insecure,
    IReadOnlyList<SpecPartManifest>? SpecSources = null)
{
    /// <summary>
    /// Resolved parts: explicit <see cref="SpecSources"/> when non-empty; otherwise a single
    /// synthetic part from <see cref="SpecSource"/> (SC-005 / FR-003).
    /// </summary>
    public IReadOnlyList<SpecPartManifest> GetSpecSources()
    {
        if (SpecSources is { Count: > 0 })
        {
            return SpecSources;
        }

        var id = DeriveLegacyPartId(Name, SpecSource);
        return [new SpecPartManifest(id, SpecSource, Label: id)];
    }

    /// <summary>
    /// Derives a stable default part id from skill name and/or source path (legacy loader / FR-003).
    /// </summary>
    public static string DeriveLegacyPartId(string skillName, string specSource)
    {
        try
        {
            if (Uri.TryCreate(specSource, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var segment = uri.Segments.LastOrDefault(s => s.Length > 0 && s != "/") ?? skillName;
                return Slug.Create(Path.GetFileNameWithoutExtension(segment.TrimEnd('/')));
            }
        }
        catch
        {
            // Fall through to path/name slug.
        }

        if (!string.IsNullOrWhiteSpace(specSource) && specSource != "-")
        {
            var file = Path.GetFileNameWithoutExtension(specSource.Trim());
            if (!string.IsNullOrWhiteSpace(file))
            {
                return Slug.Create(file);
            }
        }

        return Slug.Create(skillName);
    }
}

public static class SkillManifestIo
{
    public const string FileName = ".api2skill.json";

    public static string Serialize(SkillManifest manifest)
    {
        var parts = manifest.GetSpecSources();
        var specSourcesArray = new JsonArray();
        foreach (var part in parts)
        {
            var obj = new JsonObject
            {
                ["id"] = part.Id,
                ["source"] = part.Source,
            };
            if (!string.IsNullOrWhiteSpace(part.Label) && part.Label != part.Id)
            {
                obj["label"] = part.Label;
            }

            specSourcesArray.Add((JsonNode)obj);
        }

        var root = new JsonObject
        {
            ["name"] = manifest.Name,
            // Keep singular for single-part / older readers; always emit v2 array (DD2).
            ["specSource"] = parts[0].Source,
            ["specSources"] = specSourcesArray,
            ["scriptKind"] = manifest.ScriptKind,
            ["include"] = new JsonArray([.. manifest.Include.Select(v => (JsonNode)v)]),
            ["exclude"] = new JsonArray([.. manifest.Exclude.Select(v => (JsonNode)v)]),
            ["format"] = manifest.Format,
            ["baseUrl"] = manifest.BaseUrl,
            ["insecure"] = manifest.Insecure,
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Returns <c>null</c> when the manifest is missing or not valid JSON — both treated as "not an api2skill skill" by <c>update</c>.</summary>
    public static SkillManifest? TryLoad(string skillDirectory)
    {
        var path = Path.Combine(skillDirectory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            if (!root.TryGetProperty("name", out var nameEl) || nameEl.GetString() is not { Length: > 0 } name)
            {
                return null;
            }

            if (!root.TryGetProperty("scriptKind", out var scriptEl) || scriptEl.GetString() is not { Length: > 0 } scriptKind)
            {
                return null;
            }

            var include = ReadStringArray(root, "include");
            var exclude = ReadStringArray(root, "exclude");
            var format = root.TryGetProperty("format", out var formatEl) ? formatEl.GetString() : null;
            var baseUrl = root.TryGetProperty("baseUrl", out var baseUrlEl) ? baseUrlEl.GetString() : null;
            var insecure = root.TryGetProperty("insecure", out var insecureEl) && insecureEl.ValueKind == JsonValueKind.True;

            IReadOnlyList<SpecPartManifest>? specSources = null;
            string? specSource = null;

            if (root.TryGetProperty("specSources", out var sourcesEl)
                && sourcesEl.ValueKind == JsonValueKind.Array
                && sourcesEl.GetArrayLength() > 0)
            {
                var list = new List<SpecPartManifest>();
                foreach (var item in sourcesEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!item.TryGetProperty("id", out var idEl) || idEl.GetString() is not { Length: > 0 } id)
                    {
                        return null;
                    }

                    if (!item.TryGetProperty("source", out var srcEl) || srcEl.GetString() is not { Length: > 0 } source)
                    {
                        return null;
                    }

                    var label = item.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : null;
                    list.Add(new SpecPartManifest(id, source, label));
                }

                if (list.Count == 0)
                {
                    return null;
                }

                specSources = list;
                specSource = list[0].Source;
            }
            else if (root.TryGetProperty("specSource", out var specEl) && specEl.GetString() is { Length: > 0 } legacySource)
            {
                specSource = legacySource;
                var id = SkillManifest.DeriveLegacyPartId(name, legacySource);
                specSources = [new SpecPartManifest(id, legacySource, Label: id)];
            }
            else
            {
                return null;
            }

            return new SkillManifest(
                name,
                specSource!,
                scriptKind,
                include,
                exclude,
                format,
                baseUrl,
                insecure,
                specSources);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string> ReadStringArray(JsonElement root, string property)
    {
        var result = new List<string>();
        if (root.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.GetString() is { } s)
                {
                    result.Add(s);
                }
            }
        }

        return result;
    }
}
