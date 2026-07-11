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
            if (!string.IsNullOrWhiteSpace(body.SchemaSummary))
            {
                sb.AppendLine($"- Shape: {body.SchemaSummary}");
            }
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
                sb.AppendLine($"- `{r.StatusCode}`: {r.Description ?? string.Empty}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }
}
