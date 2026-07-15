using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// Writes raw OpenAPI component schemas to <c>reference/schemas/&lt;Name&gt;.json</c>
/// from <see cref="SkillModel.ComponentSchemas"/> (feature 011 / Constitution III —
/// receives pre-serialized JSON only; no Microsoft.OpenApi dependency).
/// </summary>
public static class SchemaWriter
{
    public static void Write(SkillModel model, DirectoryInfo skillDirectory)
    {
        var schemas = model.ComponentSchemas ?? [];
        if (schemas.Count == 0)
        {
            return;
        }

        var schemasDirectory = Directory.CreateDirectory(
            Path.Combine(skillDirectory.FullName, "reference", "schemas"));

        foreach (var schema in schemas.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            // Component names come from OpenAPI keys; reject path separators defensively.
            if (schema.Name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) >= 0)
            {
                continue;
            }

            var path = Path.Combine(schemasDirectory.FullName, $"{schema.Name}.json");
            File.WriteAllText(path, schema.RawJson);
        }
    }
}
