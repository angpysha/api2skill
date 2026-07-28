using System.Text.Json;
using System.Text.Json.Nodes;
using Api2Skill.Emit;
using Api2Skill.Examples;
using Api2Skill.Output;

namespace Api2Skill.Cli;

/// <summary>
/// Unions gitignored credential/cache files across skill directories for <c>merge</c> (D9).
/// Reads secrets only to write into a staging preserve directory — never embeds them in
/// committed outputs (constitution IV). First source wins on key conflict + warning.
/// </summary>
public static class SkillCredentialUnion
{
    public const string AuthConfigFileName = "auth.json";

    public sealed record Result(string PreserveDirectory, IReadOnlyList<string> Warnings);

    /// <summary>
    /// Builds a temporary directory with unioned <c>secrets.json</c>, <c>.auth-cache.json</c>,
    /// chosen <c>auth.json</c>, and unioned <c>examples/</c> for <see cref="Output.SkillWriter"/>.
    /// </summary>
    /// <param name="sourceSkillDirs">Ordered skill directories (first wins on conflicts).</param>
    /// <param name="existingTargetDir">
    /// When forcing into an existing skill, pass the target so its <c>auth.json</c> is preserved
    /// and it is first in the secrets/cache union order.
    /// </param>
    public static Result PreparePreserveDirectory(
        IReadOnlyList<string> sourceSkillDirs,
        string? existingTargetDir,
        string stagingParentDirectory)
    {
        ArgumentNullException.ThrowIfNull(sourceSkillDirs);
        if (sourceSkillDirs.Count == 0)
        {
            throw new ArgumentException("At least one source skill directory is required.", nameof(sourceSkillDirs));
        }

        var warnings = new List<string>();
        var preserveDir = Path.Combine(
            stagingParentDirectory,
            $".api2skill-merge-preserve-{Guid.NewGuid():N}");
        Directory.CreateDirectory(preserveDir);

        var unionOrder = new List<string>();
        if (existingTargetDir is { Length: > 0 }
            && Directory.Exists(existingTargetDir)
            && File.Exists(Path.Combine(existingTargetDir, SkillManifestIo.FileName)))
        {
            unionOrder.Add(Path.GetFullPath(existingTargetDir));
        }

        foreach (var src in sourceSkillDirs)
        {
            var full = Path.GetFullPath(src);
            if (!unionOrder.Contains(full, StringComparer.Ordinal))
            {
                unionOrder.Add(full);
            }
        }

        UnionJsonObjectFile(
            unionOrder,
            SecretsScaffold.RealSecretsFileName,
            Path.Combine(preserveDir, SecretsScaffold.RealSecretsFileName),
            warnings);

        UnionJsonObjectFile(
            unionOrder,
            SecretsScaffold.TokenCacheFileName,
            Path.Combine(preserveDir, SecretsScaffold.TokenCacheFileName),
            warnings);

        // auth.json: preserve existing target; else first source that has one.
        if (existingTargetDir is { Length: > 0 })
        {
            var targetAuth = Path.Combine(Path.GetFullPath(existingTargetDir), AuthConfigFileName);
            if (File.Exists(targetAuth))
            {
                File.Copy(targetAuth, Path.Combine(preserveDir, AuthConfigFileName), overwrite: true);
            }
            else
            {
                CopyFirstAuthJson(sourceSkillDirs, preserveDir, warnings);
            }
        }
        else
        {
            CopyFirstAuthJson(sourceSkillDirs, preserveDir, warnings);
        }

        // examples/: first-wins by operationId directory (copy later sources first, then earlier overwrite).
        for (var i = unionOrder.Count - 1; i >= 0; i--)
        {
            ExampleStore.CopyTree(unionOrder[i], preserveDir);
        }

        return new Result(preserveDir, warnings);
    }

    private static void CopyFirstAuthJson(
        IReadOnlyList<string> sourceSkillDirs,
        string preserveDir,
        List<string> warnings)
    {
        string? chosen = null;
        string? chosenPath = null;
        foreach (var src in sourceSkillDirs)
        {
            var path = Path.Combine(Path.GetFullPath(src), AuthConfigFileName);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            if (chosen is null)
            {
                chosen = text;
                chosenPath = path;
                File.WriteAllText(Path.Combine(preserveDir, AuthConfigFileName), text);
                continue;
            }

            if (!string.Equals(NormalizeJson(chosen), NormalizeJson(text), StringComparison.Ordinal))
            {
                warnings.Add(
                    $"auth.json differs between sources; using '{chosenPath}'. " +
                    $"Ignoring differing file at '{path}'.");
            }
        }
    }

    private static string NormalizeJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static void UnionJsonObjectFile(
        IReadOnlyList<string> skillDirs,
        string fileName,
        string destPath,
        List<string> warnings)
    {
        JsonObject? merged = null;

        foreach (var dir in skillDirs)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            JsonObject? incoming;
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path));
                incoming = node as JsonObject;
                if (incoming is null)
                {
                    warnings.Add($"Skipping '{path}': root is not a JSON object.");
                    continue;
                }
            }
            catch (JsonException ex)
            {
                warnings.Add($"Skipping '{path}': {ex.Message}");
                continue;
            }

            if (merged is null)
            {
                merged = incoming.DeepClone().AsObject();
                continue;
            }

            foreach (var (key, value) in incoming)
            {
                if (merged.ContainsKey(key))
                {
                    var existing = merged[key]?.ToJsonString();
                    var next = value?.ToJsonString();
                    if (!string.Equals(existing, next, StringComparison.Ordinal))
                    {
                        warnings.Add(
                            $"Credential key '{key}' in {fileName} conflicts across sources; " +
                            "keeping the first source's value (D9).");
                    }

                    continue;
                }

                merged[key] = value?.DeepClone();
            }
        }

        if (merged is not null)
        {
            File.WriteAllText(
                destPath,
                merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
