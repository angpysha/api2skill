using System.CommandLine;
using System.Linq;
using Api2Skill.Output;

namespace Api2Skill.Cli;

/// <summary>
/// <c>update</c> (specs/003-skill-update-command): regenerates a previously generated skill from
/// a newer spec, reusing the options recorded in its <c>.api2skill.json</c> manifest. Pure
/// delegation — loads the manifest, reconstructs the equivalent <see cref="GenerateOptions"/>,
/// and hands off to <see cref="GenerateCommand.RunAsync"/> (FR-003/plan.md "delegation over
/// duplication"). Never changes auth configuration (FR-008): <c>AuthConfigPath</c>/
/// <c>AuthShorthand</c> are always <see langword="null"/>, so <c>SkillWriter</c>'s existing
/// preserve-unless-a-new-one-is-supplied rule keeps whatever <c>auth.json</c> is already there.
///
/// specs/004-skill-rename-move-on-update extends this with optional <c>--name</c>/<c>--out</c>
/// overrides. When <c>--out</c> resolves to a directory different from <c>skill-path</c>,
/// credential/cache files are read from the source directory before generation (via
/// <see cref="GenerateCommand.RunAsync"/>'s <c>preserveFromDirectory</c> parameter) and the
/// source directory is removed on success (FR-003/FR-004).
///
/// Feature 012: multi-part manifests refresh all <c>specSources</c>; positional + <c>--spec-id</c>
/// replaces one part when N&gt;1 (D10).
/// </summary>
public static class UpdateCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string>("skill-path")
        {
            Description = "Path to a previously generated skill directory (must contain .api2skill.json).",
        };
        var specArgument = new Argument<string?>("spec-source")
        {
            Description = "New spec source (file, URL, or '-'). Defaults to the source recorded at generation time.",
            DefaultValueFactory = _ => null,
        };
        var nameOption = new Option<string?>("--name")
        {
            Description = "Rename the skill (same semantics as 'generate --name'). Defaults to the name recorded in .api2skill.json.",
        };
        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Relocate the skill to a new output directory (same semantics as 'generate --out'). Defaults to <skill-path> (update in place).",
        };
        var specIdOption = new Option<string?>("--spec-id")
        {
            Description = "When the skill has multiple spec parts, required with a positional spec-source to select which part to replace (D10).",
        };

        var command = new Command("update", "Regenerate a previously generated skill from a newer OpenAPI/Swagger document, reusing its saved generation options.")
        {
            pathArgument,
            specArgument,
            nameOption,
            outOption,
            specIdOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var skillPath = parseResult.GetValue(pathArgument)!;
            var specSource = parseResult.GetValue(specArgument);
            var newName = parseResult.GetValue(nameOption);
            var newOutputDirectory = parseResult.GetValue(outOption);
            var specId = parseResult.GetValue(specIdOption);
            return await RunAsync(skillPath, specSource, cancellationToken, newName, newOutputDirectory, specId).ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(
        string skillPath, string? newSpecSource, CancellationToken cancellationToken,
        string? newName = null, string? newOutputDirectory = null, string? specId = null)
    {
        var manifest = SkillManifestIo.TryLoad(skillPath);
        if (manifest is null)
        {
            Console.Error.WriteLine(
                $"'{skillPath}' does not look like an api2skill-generated skill — no readable {SkillManifestIo.FileName} found. " +
                "Use 'api2skill generate' to create it, or regenerate once with a current version of api2skill to add the manifest.");
            return ExitCodes.UsageError;
        }

        var sourceDir = Path.GetFullPath(skillPath);
        var targetDir = newOutputDirectory is { Length: > 0 } o ? Path.GetFullPath(o) : sourceDir;
        var isRelocating = !string.Equals(sourceDir, targetDir, StringComparison.Ordinal);

        if (isRelocating && Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
        {
            Console.Error.WriteLine(
                $"'{targetDir}' already exists and is not empty. Choose an empty or nonexistent --out directory, " +
                "or omit --out to update in place.");
            return ExitCodes.UsageError;
        }

        var parts = manifest.GetSpecSources().ToList();

        if (newSpecSource is { Length: > 0 })
        {
            if (parts.Count > 1)
            {
                if (specId is not { Length: > 0 })
                {
                    Console.Error.WriteLine(
                        "This skill has multiple spec parts. Pass --spec-id <id> with the positional spec-source " +
                        "to replace one part, or omit the positional source to refresh all parts from the manifest.");
                    return ExitCodes.UsageError;
                }

                var id = Slug.Create(specId);
                var idx = parts.FindIndex(p => string.Equals(p.Id, id, StringComparison.Ordinal));
                if (idx < 0)
                {
                    Console.Error.WriteLine(
                        $"Unknown --spec-id '{id}'. Known parts: {string.Join(", ", parts.Select(p => p.Id))}.");
                    return ExitCodes.UsageError;
                }

                var old = parts[idx];
                parts[idx] = old with { Source = newSpecSource };
            }
            else
            {
                // Single-part / legacy: replace the sole source (003 behavior).
                var only = parts[0];
                parts[0] = only with { Source = newSpecSource };
            }
        }
        else if (specId is { Length: > 0 })
        {
            Console.Error.WriteLine(
                "--spec-id requires a positional <spec-source> when replacing one part. " +
                "Omit both to refresh all parts from the manifest.");
            return ExitCodes.UsageError;
        }

        GenerateOptions options;
        if (parts.Count >= 2)
        {
            var generateParts = parts
                .Select(p => new GenerateSpecPart(p.Source, p.Id, p.Label))
                .ToList();
            options = new GenerateOptions(
                SpecSource: generateParts[0].Source,
                OutputDirectory: targetDir,
                Name: newName ?? manifest.Name,
                ScriptKind: manifest.ScriptKind,
                Include: manifest.Include,
                Exclude: manifest.Exclude,
                Force: true,
                Insecure: manifest.Insecure,
                Format: manifest.Format,
                BaseUrl: manifest.BaseUrl,
                AuthConfigPath: null,
                AuthShorthand: null,
                Login: false,
                SpecParts: generateParts);
        }
        else
        {
            // Keep single-part path identical to pre-012 update (goldens / SC-005).
            options = new GenerateOptions(
                SpecSource: parts[0].Source,
                OutputDirectory: targetDir,
                Name: newName ?? manifest.Name,
                ScriptKind: manifest.ScriptKind,
                Include: manifest.Include,
                Exclude: manifest.Exclude,
                Force: true,
                Insecure: manifest.Insecure,
                Format: manifest.Format,
                BaseUrl: manifest.BaseUrl,
                AuthConfigPath: null,
                AuthShorthand: null,
                Login: false);
        }

        var exitCode = await GenerateCommand.RunAsync(
            options, cancellationToken, preserveFromDirectory: isRelocating ? sourceDir : null).ConfigureAwait(false);

        if (exitCode == ExitCodes.Success && isRelocating)
        {
            try
            {
                Directory.Delete(sourceDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // spec.md edge case: a good regeneration at targetDir must never be rolled back
                // just because cleaning up the old directory failed — surface it and move on.
                Console.Error.WriteLine(
                    $"warning: updated skill written to '{targetDir}', but removing the old directory " +
                    $"'{sourceDir}' failed: {ex.Message}. Delete it manually.");
            }
        }

        return exitCode;
    }
}
