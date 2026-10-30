using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// Writes one JSON document per tag (endpoint group) at
/// <c>reference/schemas/&lt;tag&gt;.json</c>. Each file is a map of component name to the raw
/// OpenAPI schema object, so a skill does not grow one file per schema.
/// </summary>
public static class SchemaWriter
{
    private static readonly Regex SchemaRef = new(
        @"#/components/schemas/([^""\\\s]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static void Write(SkillModel model, DirectoryInfo skillDirectory)
    {
        var byName = (model.ComponentSchemas ?? [])
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        if (byName.Count == 0 || model.Tags.Count == 0)
        {
            return;
        }

        var stems = ReferencePaths.BuildTagFileStems(model.Tags.Select(t => t.Tag));
        var schemasDirectory = Directory.CreateDirectory(
            Path.Combine(skillDirectory.FullName, "reference", "schemas"));

        foreach (var tag in model.Tags)
        {
            var names = ReachableNames(tag, byName);
            if (names.Count == 0)
            {
                continue;
            }

            var root = new JsonObject();
            foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
            {
                root[name] = JsonNode.Parse(byName[name].RawJson);
            }

            var path = Path.Combine(schemasDirectory.FullName, $"{stems[tag.Tag]}.json");
            File.WriteAllText(path, root.ToJsonString(Indented) + Environment.NewLine);
        }
    }

    /// <summary>Schema file linked from <c>reference/&lt;tag&gt;.md</c>, relative to that file.</summary>
    public static string RelativePathForTag(string tagStem) => $"schemas/{tagStem}.json";

    private static HashSet<string> ReachableNames(TagGroup tag, IReadOnlyDictionary<string, ComponentSchemaModel> byName)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();

        foreach (var op in tag.Operations)
        {
            Enqueue(op.RequestBody?.SchemaName, queue, found, byName);
            Enqueue(op.RequestBody?.Schema?.SchemaName, queue, found, byName);
            EnqueueVariants(op.RequestBody?.Schema, queue, found, byName);
            foreach (var parameter in op.Parameters)
            {
                Enqueue(parameter.Schema?.SchemaName, queue, found, byName);
                EnqueueVariants(parameter.Schema, queue, found, byName);
            }

            foreach (var response in op.Responses)
            {
                Enqueue(response.SchemaName, queue, found, byName);
                Enqueue(response.Schema?.SchemaName, queue, found, byName);
                EnqueueVariants(response.Schema, queue, found, byName);
            }
        }

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            foreach (Match match in SchemaRef.Matches(byName[name].RawJson))
            {
                Enqueue(match.Groups[1].Value, queue, found, byName);
            }
        }

        return found;
    }

    private static void EnqueueVariants(
        SchemaDetailModel? schema,
        Queue<string> queue,
        HashSet<string> found,
        IReadOnlyDictionary<string, ComponentSchemaModel> byName)
    {
        if (schema?.Variants is not { Count: > 0 } variants)
        {
            return;
        }

        foreach (var variant in variants)
        {
            Enqueue(variant.SchemaName, queue, found, byName);
        }
    }

    private static void Enqueue(
        string? name,
        Queue<string> queue,
        HashSet<string> found,
        IReadOnlyDictionary<string, ComponentSchemaModel> byName)
    {
        if (string.IsNullOrWhiteSpace(name) || !byName.ContainsKey(name) || !found.Add(name))
        {
            return;
        }

        queue.Enqueue(name);
    }
}
