using System.Text;
using Api2Skill.Auth;
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
        sb.AppendLine(model.InsecureDefault
            ? "Untrusted HTTPS (self-signed/invalid certificates) is **on by default for this skill** (generated with `--insecure`) — **dev/local use only**, never in production. Set `API2SKILL_INSECURE=0` to require valid certificates instead."
            : "Untrusted HTTPS (self-signed/invalid certificates) is **off by default**. Set `API2SKILL_INSECURE=1` to accept them — **dev/local use only**, never in production.");
        sb.AppendLine();

        if (model.SecuritySchemes.Count > 0)
        {
            sb.AppendLine("## Auth (from the spec)");
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

        if (model.AuthScaffoldGuidance is { } scaffoldGuidance)
        {
            sb.AppendLine("## Auth profile names");
            sb.AppendLine();
            sb.AppendLine(
                "An inactive `auth.json` template was written on first generate. Profile names must match OpenAPI security scheme IDs for explicit auth to attach correctly.");
            sb.AppendLine();
            sb.AppendLine("| scheme ID | profile name | status | operations / tags |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var entry in scaffoldGuidance.Schemes)
            {
                var status = entry.Status == SchemeScaffoldStatus.Scaffolded ? "scaffolded" : "manual only";
                var ops = entry.OperationIds.Count > 0 ? string.Join(", ", entry.OperationIds) : "(none)";
                var tags = entry.Tags.Count > 0 ? string.Join(", ", entry.Tags) : "(none)";
                sb.AppendLine(
                    $"| `{entry.SchemeId}` | `{entry.SuggestedProfileName}` | {status} | ops: {ops}; tags: {tags} |");
            }
            sb.AppendLine();
            sb.AppendLine(
                "Activate explicit auth after editing `auth.json`: `api2skill generate <spec> --auth-config ./auth.json --force`");
            sb.AppendLine();
        }

        if (model.AuthConfig is { Profiles.Count: > 0 } authConfig)
        {
            sb.AppendLine("## Explicit auth profiles (auth.json)");
            sb.AppendLine();
            sb.AppendLine("This skill's auth is explicitly configured in the committed `auth.json`, overriding the spec-derived auth above for any operation an attached profile covers. Referenced `{secret:NAME}` values are resolved from `secrets.json` at call time — never commit real values.");
            sb.AppendLine();
            sb.AppendLine("| profile | type | attach | notes |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var profile in authConfig.Profiles)
            {
                var attach = profile.Attach.Scope == AttachScope.Global
                    ? "global"
                    : $"tags: {string.Join(", ", profile.Attach.Tags)}";
                sb.AppendLine($"| `{profile.Name}` | {ProfileTypeLabel(profile.Type)} | {attach} | {ProfileNotes(profile)} |");
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

    private static string ProfileTypeLabel(AuthType type) => type switch
    {
        AuthType.Bearer => "bearer",
        AuthType.Basic => "basic",
        AuthType.Custom => "custom",
        AuthType.Script => "script",
        AuthType.OAuth2 => "oauth2",
        _ => type.ToString(),
    };

    private static string ProfileNotes(AuthProfile profile) => profile.Type switch
    {
        AuthType.Bearer => "Set the token in `secrets.json`; `Bearer ` is added automatically if missing.",
        AuthType.Basic => "Set `username`/`password` in `secrets.json`; sent as `Authorization: Basic ...`.",
        AuthType.Custom => $"Sends {profile.Custom!.Headers.Count} header(s): {string.Join(", ", profile.Custom.Headers.Select(h => h.Name))}. Set the referenced values in `secrets.json`.",
        AuthType.Script => $"Runs the user-provided local command `{profile.Script!.Command}` on every call; its trimmed stdout becomes the `{profile.Script.Header}` header{(profile.Script.BearerPrefix ? " (`Bearer ` added when absent)" : "")}.",
        AuthType.OAuth2 => "OAuth2 profile (see `auth.json` for endpoints/scopes). **Interactive login is not yet executed by this generated dispatcher version.**",
        _ => string.Empty,
    };
}
