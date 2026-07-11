using System.Text;
using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// Writes the compact, always-loaded <c>SKILL.md</c> — overview, auth setup, and a
/// tag-grouped operation index only. Full per-operation detail lives in
/// <see cref="ReferenceWriter"/>'s output (progressive disclosure, D6/FR-004).
/// </summary>
public static class SkillMdWriter
{
    public static void Write(SkillModel model, DirectoryInfo skillDirectory, IScriptEmitter emitter)
    {
        var sb = new StringBuilder();

        sb.AppendLine("---");
        sb.AppendLine($"name: \"{model.Name}\"");
        sb.AppendLine($"description: \"{EscapeYamlString(Describe(model))}\"");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {model.Title}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.Description))
        {
            sb.AppendLine(model.Description);
            sb.AppendLine();
        }

        sb.AppendLine("## Overview");
        sb.AppendLine();
        sb.AppendLine(model.BaseUrl is { Length: > 0 }
            ? $"Base URL: `{model.BaseUrl}`"
            : "No base URL is defined in the spec — supply one via `baseUrl` in `secrets.json` or `--base-url` at generation time.");
        sb.AppendLine();

        sb.AppendLine("## Setup");
        sb.AppendLine();
        sb.AppendLine($"Runner: `{emitter.RunnerDescription}`");
        sb.AppendLine();
        sb.AppendLine("Copy `secrets.example.json` to `secrets.json` and fill in real credentials before calling authenticated operations. `secrets.json` is gitignored — never commit it.");
        sb.AppendLine();
        sb.AppendLine("Untrusted HTTPS (self-signed/invalid certificates) is **off by default**. Set `API2SKILL_INSECURE=1` to accept them — **dev/local use only**, never in production.");
        sb.AppendLine();

        if (model.SecuritySchemes.Count > 0)
        {
            sb.AppendLine("## Auth");
            sb.AppendLine();
            sb.AppendLine("| scheme | kind | secrets.json keys |");
            sb.AppendLine("|---|---|---|");
            foreach (var scheme in model.SecuritySchemes)
            {
                var keys = scheme.SecretKeys.Count > 0 ? string.Join(", ", scheme.SecretKeys) : "(none — manual auth required)";
                sb.AppendLine($"| `{scheme.Id}` | {scheme.Kind} | {keys} |");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## How to call");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine($"{emitter.RunnerDescription} <operationId> [--<param> <value> ...] [--body <json|@file>]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Operations");
        sb.AppendLine();
        foreach (var tag in model.Tags)
        {
            sb.AppendLine($"### {tag.Tag}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(tag.Summary))
            {
                sb.AppendLine(tag.Summary);
                sb.AppendLine();
            }

            sb.AppendLine("| operationId | method | path | summary | reference |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var op in tag.Operations)
            {
                var summary = EscapeTableCell(op.Summary ?? op.Description ?? string.Empty);
                sb.AppendLine($"| `{op.OperationId}` | {op.Method.Method} | `{op.PathTemplate}` | {summary} | [reference/{tag.Tag}.md](reference/{tag.Tag}.md) |");
            }
            sb.AppendLine();
        }

        if (model.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            sb.AppendLine();
            foreach (var warning in model.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
            sb.AppendLine();
        }

        File.WriteAllText(Path.Combine(skillDirectory.FullName, "SKILL.md"), sb.ToString());
    }

    private static string Describe(SkillModel model)
    {
        var desc = $"Call the {model.Title} API.";
        return string.IsNullOrWhiteSpace(model.Description) ? desc : $"{desc} {model.Description}";
    }

    private static string EscapeYamlString(string value) => value.Replace("\"", "'").Replace("\n", " ").Trim();

    private static string EscapeTableCell(string value) => value.Replace("|", "\\|").Replace("\n", " ").Trim();
}
