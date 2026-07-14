using System.Text;
using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// Writes the on-demand <c>reference/&lt;tag&gt;.md</c> files — full parameter/schema/response
/// detail per tag, loaded only when Claude needs it (progressive disclosure, D6/FR-004).
/// </summary>
public static class ReferenceWriter
{
    public static void Write(SkillModel model, DirectoryInfo skillDirectory)
    {
        var referenceDirectory = Directory.CreateDirectory(Path.Combine(skillDirectory.FullName, "reference"));

        foreach (var tag in model.Tags)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {tag.Tag}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(tag.Summary))
            {
                sb.AppendLine(tag.Summary);
                sb.AppendLine();
            }

            foreach (var op in tag.Operations)
            {
                WriteOperation(sb, op);
            }

            File.WriteAllText(Path.Combine(referenceDirectory.FullName, $"{tag.Tag}.md"), sb.ToString());
        }
    }

    private static void WriteOperation(StringBuilder sb, OperationModel op)
    {
        sb.AppendLine($"## {op.OperationId}");
        sb.AppendLine();
        sb.AppendLine($"`{op.Method.Method} {op.PathTemplate}`");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(op.Summary))
        {
            sb.AppendLine(op.Summary);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(op.Description) && op.Description != op.Summary)
        {
            sb.AppendLine(op.Description);
            sb.AppendLine();
        }

        if (op.Parameters.Count > 0)
        {
            sb.AppendLine("**Parameters**");
            sb.AppendLine();
            sb.AppendLine("| name | in | required | type | description |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var p in op.Parameters)
            {
                sb.AppendLine($"| `{p.Name}` | {p.In} | {(p.Required ? "yes" : "no")} | {p.Type} | {p.Description ?? string.Empty} |");
            }
            sb.AppendLine();
        }

        if (op.RequestBody is { } body)
        {
            sb.AppendLine("**Request body**");
            sb.AppendLine();
            sb.AppendLine($"- Content-Type: `{body.ContentType}`{(body.Required ? " (required)" : " (optional)")}");
            WriteSchemaDetail(sb, body.SchemaSummary, body.Schema);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("**Request body**: none");
            sb.AppendLine();
        }

        sb.AppendLine("**Auth**: " + (op.SecuritySchemeIds.Count > 0
            ? string.Join(", ", op.SecuritySchemeIds.Select(id => $"`{id}`"))
            : "none"));
        sb.AppendLine();

        if (op.Responses.Count > 0)
        {
            sb.AppendLine("**Responses**");
            sb.AppendLine();
            foreach (var r in op.Responses)
            {
                sb.AppendLine($"### `{r.StatusCode}`{(string.IsNullOrWhiteSpace(r.Description) ? string.Empty : $": {r.Description}")}");
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(r.ContentType))
                {
                    sb.AppendLine($"- Content-Type: `{r.ContentType}`");
                }

                WriteSchemaDetail(sb, r.SchemaSummary, r.Schema);
                if (string.IsNullOrWhiteSpace(r.ContentType) && r.Schema is null && string.IsNullOrWhiteSpace(r.SchemaSummary))
                {
                    sb.AppendLine("- Body: none documented in the OpenAPI response");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void WriteSchemaDetail(StringBuilder sb, string? summary, SchemaDetailModel? schema)
    {
        var shape = schema?.Summary ?? summary;
        if (!string.IsNullOrWhiteSpace(shape))
        {
            sb.AppendLine($"- Shape: {shape}");
        }

        if (schema is { Properties.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("| property | type | required | description |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var p in schema.Properties)
            {
                var type = string.IsNullOrWhiteSpace(p.Format) ? p.Type : $"{p.Type} ({p.Format})";
                sb.AppendLine($"| `{p.Name}` | {type} | {(p.Required ? "yes" : "no")} | {p.Description ?? string.Empty} |");
            }
        }

        if (schema is { ExampleJson: { Length: > 0 } example })
        {
            sb.AppendLine();
            sb.AppendLine("Example:");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(example);
            sb.AppendLine("```");
        }
    }
}
