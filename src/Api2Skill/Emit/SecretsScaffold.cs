using System.Text.Json;
using System.Text.Json.Nodes;
using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// Writes <c>secrets.example.json</c> (committed template — placeholder values only) and a
/// <c>.gitignore</c> excluding the real <c>secrets.json</c>, one entry per security scheme
/// actually referenced by the model's operations (Constitution IV, FR-003b, data-model.md
/// "Derived: Secrets schema"). Never reads or embeds a real credential.
/// </summary>
public static class SecretsScaffold
{
    public const string RealSecretsFileName = "secrets.json";
    public const string TemplateFileName = "secrets.example.json";

    public static void Write(SkillModel model, DirectoryInfo skillDirectory)
    {
        var root = new JsonObject
        {
            ["$comment"] = "Fill in real values and save as secrets.json next to this file (gitignored). Never commit real credentials.",
        };

        foreach (var scheme in model.SecuritySchemes)
        {
            if (scheme.SecretKeys.Count == 0)
            {
                // Unsupported scheme (EC-6) — nothing to scaffold; SKILL.md already flags it.
                continue;
            }

            var entry = new JsonObject();
            foreach (var key in scheme.SecretKeys)
            {
                entry[key] = key == "tokenUrl" ? scheme.OAuthTokenUrl ?? string.Empty : string.Empty;
            }
            if (scheme.OAuthScopes.Count > 0)
            {
                entry["scopes"] = new JsonArray([.. scheme.OAuthScopes.Select(s => (JsonNode)s)]);
            }
            root[scheme.Id] = entry;
        }

        if (string.IsNullOrWhiteSpace(model.BaseUrl))
        {
            root["baseUrl"] = string.Empty;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(skillDirectory.FullName, TemplateFileName), root.ToJsonString(options));
        File.WriteAllText(Path.Combine(skillDirectory.FullName, ".gitignore"), RealSecretsFileName + Environment.NewLine);
    }
}
