using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api2Skill.Output;

/// <summary>
/// The options used to produce a given generated skill, recorded so <c>update</c> can regenerate
/// it later without the caller re-supplying every original flag (spec.md FR-001). Secret-free —
/// never records resolved credentials, only the same non-secret options <c>generate</c> itself
/// was given.
/// </summary>
public sealed record SkillManifest(
    string Name,
    string SpecSource,
    string ScriptKind,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude,
    string? Format,
    string? BaseUrl,
    bool Insecure);

public static class SkillManifestIo
{
    public const string FileName = ".api2skill.json";

    public static string Serialize(SkillManifest manifest)
    {
        var root = new JsonObject
        {
            ["name"] = manifest.Name,
            ["specSource"] = manifest.SpecSource,
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
            if (!root.TryGetProperty("specSource", out var specEl) || specEl.GetString() is not { Length: > 0 } specSource)
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

            return new SkillManifest(name, specSource, scriptKind, include, exclude, format, baseUrl, insecure);
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
