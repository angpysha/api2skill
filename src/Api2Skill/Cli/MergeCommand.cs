using System.CommandLine;
using Api2Skill.Model;
using Api2Skill.Output;

namespace Api2Skill.Cli;

/// <summary>
/// <c>merge --into</c> (012 US2 / D8–D9): compose ≥2 existing skill directories into one target.
/// Sources are never deleted. Credential union is first-wins with warnings.
/// Not for appending a single OpenAPI file — use <c>add-spec</c>.
/// </summary>
public static class MergeCommand
{
    public static Command Create()
    {
        var intoOption = new Option<string>("--into")
        {
            Description = "Target directory for the merged skill (created if missing).",
            Required = true,
        };
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Overwrite an existing api2skill skill at --into.",
            DefaultValueFactory = _ => false,
        };
        var onConflictOption = new Option<string>("--on-conflict")
        {
            Description = "operationId collision policy across parts: fail (default) or prefix.",
            DefaultValueFactory = _ => "fail",
        };
        var skillsArgument = new Argument<string[]>("skill-paths")
        {
            Description = "Two or more existing skill directories to merge (sources are left intact).",
            Arity = ArgumentArity.OneOrMore,
        };

        var command = new Command(
            "merge",
            "Merge two or more previously generated skills into one composite skill directory.")
        {
            intoOption,
            forceOption,
            onConflictOption,
            skillsArgument,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var into = parseResult.GetValue(intoOption)!;
            var force = parseResult.GetValue(forceOption);
            var onConflict = parseResult.GetValue(onConflictOption)!;
            var skills = parseResult.GetValue(skillsArgument) ?? [];
            return await RunAsync(into, skills, force, onConflict, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(
        string intoDirectory,
        IReadOnlyList<string> skillPaths,
        bool force,
        string onConflict,
        CancellationToken cancellationToken)
    {
        if (skillPaths.Count < 2)
        {
            Console.Error.WriteLine(
                "merge requires at least two skill directories. " +
                "To append one OpenAPI document to an existing skill, use 'api2skill add-spec' instead.");
            return ExitCodes.UsageError;
        }

        if (!string.Equals(onConflict, "fail", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(onConflict, "prefix", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown --on-conflict '{onConflict}'. Valid values: fail, prefix.");
            return ExitCodes.UsageError;
        }

        var loaded = new List<(string Dir, SkillManifest Manifest)>();
        foreach (var path in skillPaths)
        {
            var manifest = SkillManifestIo.TryLoad(path);
            if (manifest is null)
            {
                Console.Error.WriteLine(
                    $"'{path}' does not look like an api2skill-generated skill — no readable {SkillManifestIo.FileName} found.");
                return ExitCodes.UsageError;
            }

            loaded.Add((Path.GetFullPath(path), manifest));
        }

        var targetDir = Path.GetFullPath(intoDirectory);
        var targetManifest = Directory.Exists(targetDir) ? SkillManifestIo.TryLoad(targetDir) : null;
        var targetIsSkill = targetManifest is not null;

        if (Directory.Exists(targetDir))
        {
            if (targetIsSkill && !force)
            {
                Console.Error.WriteLine(
                    $"'{targetDir}' already contains an api2skill skill. Pass --force to overwrite, " +
                    "or use 'api2skill add-spec' to append a new OpenAPI document to that skill.");
                return ExitCodes.OutputExists;
            }

            if (!targetIsSkill && Directory.EnumerateFileSystemEntries(targetDir).Any() && !force)
            {
                Console.Error.WriteLine(
                    $"'{targetDir}' already exists and is not empty. Choose an empty directory, " +
                    "or pass --force only when overwriting an api2skill skill.");
                return ExitCodes.UsageError;
            }
        }

        string? canonicalScriptKind = targetIsSkill && force ? targetManifest!.ScriptKind : null;
        try
        {
            SkillModelComposer.ValidateScriptKinds(
                canonicalScriptKind,
                loaded.Select(l => (l.Manifest.Name, l.Manifest.ScriptKind)).ToList());
        }
        catch (CompositionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.UsageError;
        }

        var parts = new List<GenerateSpecPart>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (dir, manifest) in loaded)
        {
            foreach (var part in manifest.GetSpecSources())
            {
                if (!seenIds.Add(part.Id))
                {
                    Console.Error.WriteLine(
                        $"Duplicate spec-id '{part.Id}' across merge sources. " +
                        "Rename parts in source manifests or regenerate with distinct --spec-id values.");
                    return ExitCodes.UsageError;
                }

                var source = SpecCache.ResolveMergeSource(dir, part.Id, part.Source);
                if (!string.Equals(source, part.Source, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(
                        $"info: merge using cached spec for '{part.Id}' at '{source}' (original source left intact in source skill).");
                }

                parts.Add(new GenerateSpecPart(source, part.Id, part.Label));
            }
        }

        var optionsSource = targetIsSkill && force ? targetManifest! : loaded[0].Manifest;
        var skillName = Slug.Create(
            targetIsSkill && force
                ? targetManifest!.Name
                : Path.GetFileName(targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } leaf
                    ? leaf
                    : loaded[0].Manifest.Name);

        // Prefer an explicit HTTP(S) base from any source manifest over a missing/file:// value.
        string? mergeBaseUrl = null;
        foreach (var (_, manifest) in loaded)
        {
            var candidate = manifest.BaseUrl;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                    || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                mergeBaseUrl = candidate.Trim().TrimEnd('/');
                break;
            }
        }

        mergeBaseUrl ??= optionsSource.BaseUrl;
        if (mergeBaseUrl is not null
            && Uri.TryCreate(mergeBaseUrl.Trim(), UriKind.Absolute, out var badUri)
            && !badUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !badUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            mergeBaseUrl = null;
        }

        string? preserveDir = null;
        try
        {
            var union = SkillCredentialUnion.PreparePreserveDirectory(
                loaded.Select(l => l.Dir).ToList(),
                existingTargetDir: targetIsSkill && force ? targetDir : null,
                stagingParentDirectory: Path.GetDirectoryName(targetDir) ?? Directory.GetCurrentDirectory());
            preserveDir = union.PreserveDirectory;
            foreach (var warning in union.Warnings)
            {
                Console.Error.WriteLine($"warning: {warning}");
            }

            var options = new GenerateOptions(
                SpecSource: parts[0].Source,
                OutputDirectory: targetDir,
                Name: skillName,
                ScriptKind: optionsSource.ScriptKind,
                Include: optionsSource.Include,
                Exclude: optionsSource.Exclude,
                Force: force || targetIsSkill,
                Insecure: optionsSource.Insecure,
                Format: optionsSource.Format,
                BaseUrl: mergeBaseUrl,
                AuthConfigPath: null,
                AuthShorthand: null,
                Login: false,
                SpecParts: parts,
                OnConflict: onConflict);

            // Always force when target path already exists as a skill we are overwriting;
            // for a brand-new path Force may still be false — SkillWriter creates the dir.
            if (Directory.Exists(targetDir))
            {
                options = options with { Force = true };
            }

            return await GenerateCommand.RunAsync(options, cancellationToken, preserveFromDirectory: preserveDir)
                .ConfigureAwait(false);
        }
        finally
        {
            if (preserveDir is not null && Directory.Exists(preserveDir))
            {
                try
                {
                    Directory.Delete(preserveDir, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"warning: could not remove merge staging '{preserveDir}': {ex.Message}");
                }
            }
        }
    }
}
