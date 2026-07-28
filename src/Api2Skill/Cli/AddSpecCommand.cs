using System.CommandLine;
using Api2Skill.Output;

namespace Api2Skill.Cli;

/// <summary>
/// <c>add-spec</c> (012 US3 / D8): append one OpenAPI source to an existing skill and regenerate
/// in place, preserving credentials (SkillWriter --force semantics).
/// </summary>
public static class AddSpecCommand
{
    public static Command Create()
    {
        var skillArgument = new Argument<string>("skill-path")
        {
            Description = "Path to an existing skill directory (must contain .api2skill.json).",
        };
        var specArgument = new Argument<string>("spec-source")
        {
            Description = "OpenAPI/Swagger source to append (file, URL, or '-').",
        };
        var specIdOption = new Option<string?>("--spec-id")
        {
            Description = "Stable id for the new part (defaults to filename slug).",
        };
        var specLabelOption = new Option<string?>("--spec-label")
        {
            Description = "Optional display label for the new part.",
        };
        var onConflictOption = new Option<string>("--on-conflict")
        {
            Description = "operationId collision policy: fail (default) or prefix.",
            DefaultValueFactory = _ => "fail",
        };

        var command = new Command(
            "add-spec",
            "Append one OpenAPI document to an existing skill and regenerate in place.")
        {
            skillArgument,
            specArgument,
            specIdOption,
            specLabelOption,
            onConflictOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var skillPath = parseResult.GetValue(skillArgument)!;
            var specSource = parseResult.GetValue(specArgument)!;
            var specId = parseResult.GetValue(specIdOption);
            var specLabel = parseResult.GetValue(specLabelOption);
            var onConflict = parseResult.GetValue(onConflictOption)!;
            return await RunAsync(skillPath, specSource, specId, specLabel, onConflict, cancellationToken)
                .ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(
        string skillPath,
        string specSource,
        string? specId,
        string? specLabel,
        string onConflict,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(onConflict, "fail", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(onConflict, "prefix", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown --on-conflict '{onConflict}'. Valid values: fail, prefix.");
            return ExitCodes.UsageError;
        }

        var manifest = SkillManifestIo.TryLoad(skillPath);
        if (manifest is null)
        {
            Console.Error.WriteLine(
                $"'{skillPath}' does not look like an api2skill-generated skill — no readable {SkillManifestIo.FileName} found. " +
                "Use 'api2skill generate' first, or 'api2skill merge' to combine existing skills.");
            return ExitCodes.UsageError;
        }

        var existing = manifest.GetSpecSources();
        var id = specId is { Length: > 0 }
            ? Slug.Create(specId)
            : SkillManifest.DeriveLegacyPartId(manifest.Name, specSource);

        if (existing.Any(p => string.Equals(p.Id, id, StringComparison.Ordinal)))
        {
            Console.Error.WriteLine(
                $"spec-id '{id}' already exists in '{skillPath}'. Choose a different --spec-id.");
            return ExitCodes.UsageError;
        }

        var parts = existing
            .Select(p => new GenerateSpecPart(p.Source, p.Id, p.Label))
            .Append(new GenerateSpecPart(specSource, id, specLabel ?? id))
            .ToList();

        var options = new GenerateOptions(
            SpecSource: parts[0].Source,
            OutputDirectory: Path.GetFullPath(skillPath),
            Name: manifest.Name,
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
            SpecParts: parts,
            OnConflict: onConflict);

        return await GenerateCommand.RunAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
