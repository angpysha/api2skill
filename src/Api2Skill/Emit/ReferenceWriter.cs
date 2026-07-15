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
            sb.AppendLine("| name | in | required | type | enum | description |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var p in op.Parameters)
            {
                var type = FormatTypeDisplay(p.Type, p.Format);
                var enums = FormatEnumCell(p.EnumValues);
                sb.AppendLine(
                    $"| `{p.Name}` | {p.In} | {(p.Required ? "yes" : "no")} | {type} | {enums} | {p.Description ?? string.Empty} |");
            }
            sb.AppendLine();

            foreach (var p in op.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Example))
                {
                    sb.AppendLine($"- `{p.Name}` example: `{p.Example}`");
                }

                // Object/array query (or path/header) params get the same property table as bodies.
                if (p.Schema is { Properties.Count: > 0 } || p.Schema?.SchemaName is not null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"**`{p.Name}` ({p.In}) schema**");
                    sb.AppendLine();
                    WriteSchemaDetail(sb, p.Schema?.Summary, p.Schema, p.Schema?.SchemaName, emitExample: true);
                }
            }

            if (op.Parameters.Any(p => !string.IsNullOrWhiteSpace(p.Example)
                || p.Schema is { Properties.Count: > 0 }
                || p.Schema?.SchemaName is not null))
            {
                sb.AppendLine();
            }
        }

        if (op.RequestBody is { } body)
        {
            sb.AppendLine("**Request body**");
            sb.AppendLine();
            sb.AppendLine($"- Content-Type: `{body.ContentType}`{(body.Required ? " (required)" : " (optional)")}");
            WriteSchemaDetail(
                sb,
                body.SchemaSummary,
                body.Schema,
                body.SchemaName ?? body.Schema?.SchemaName,
                emitExample: IsJsonMedia(body.ContentType));
            if (!IsJsonMedia(body.ContentType))
            {
                sb.AppendLine("- Note: no pasteable JSON body emitted for this content type");
            }

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

                var emitExample = r.ContentType is { Length: > 0 } ct && IsJsonMedia(ct);
                WriteSchemaDetail(
                    sb,
                    r.SchemaSummary,
                    r.Schema,
                    r.SchemaName ?? r.Schema?.SchemaName,
                    emitExample: emitExample);
                if (r.ContentType is { Length: > 0 } && !IsJsonMedia(r.ContentType))
                {
                    sb.AppendLine("- Note: no pasteable JSON body emitted for this content type");
                }

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

    private static void WriteSchemaDetail(
        StringBuilder sb,
        string? summary,
        SchemaDetailModel? schema,
        string? schemaName,
        bool emitExample)
    {
        var name = schemaName ?? schema?.SchemaName;
        if (!string.IsNullOrWhiteSpace(name))
        {
            sb.AppendLine($"- Schema: [`{name}`](schemas/{name}.json)");
        }

        var shape = schema?.Summary ?? summary;
        if (!string.IsNullOrWhiteSpace(shape))
        {
            sb.AppendLine($"- Shape: {shape}");
        }

        if (schema?.Variants is { Count: > 0 } variants)
        {
            sb.AppendLine();
            sb.AppendLine("**Variants** (use one of):");
            sb.AppendLine();
            foreach (var v in variants)
            {
                if (!string.IsNullOrWhiteSpace(v.SchemaName))
                {
                    sb.AppendLine($"{v.Index + 1}. Schema: [`{v.SchemaName}`](schemas/{v.SchemaName}.json) — {v.Summary}");
                }
                else
                {
                    sb.AppendLine($"{v.Index + 1}. Shape: {v.Summary}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("_Pasteable Example uses the first variant._");
        }

        if (schema?.EnumValues is { Count: > 0 } rootEnums)
        {
            sb.AppendLine($"- Enum: {string.Join(", ", rootEnums.Select(e => $"`{e}`"))}");
        }

        if (schema is { Properties.Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("| property | type | required | enum | description |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var p in schema.Properties)
            {
                var type = FormatTypeDisplay(p.Type, p.Format);
                var enums = FormatEnumCell(p.EnumValues);
                sb.AppendLine(
                    $"| `{p.Name}` | {type} | {(p.Required ? "yes" : "no")} | {enums} | {p.Description ?? string.Empty} |");
            }
        }

        if (emitExample && schema is { ExampleJson: { Length: > 0 } example })
        {
            sb.AppendLine();
            sb.AppendLine("Example:");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(example.TrimEnd());
            sb.AppendLine("```");
        }

        if (schema is { Truncated: true })
        {
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(name))
            {
                sb.AppendLine(
                    $"_Nested fields truncated at depth 4 — see [`{name}`](schemas/{name}.json) for the full schema._");
            }
            else
            {
                sb.AppendLine("_Nested fields truncated at depth 4._");
            }
        }
    }

    private static bool IsJsonMedia(string contentType) =>
        contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static string FormatTypeDisplay(string type, string? format) =>
        string.IsNullOrWhiteSpace(format) ? type : $"{type} ({format})";

    private static string FormatEnumCell(IReadOnlyList<string>? values) =>
        values is { Count: > 0 }
            ? string.Join(", ", values.Select(v => $"`{v}`"))
            : string.Empty;
}
