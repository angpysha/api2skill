using System.CommandLine;
using Api2Skill.Emit;
using Api2Skill.Output;

namespace Api2Skill.Cli;

/// <summary>
/// <c>migrate</c> switches a generated skill to another <c>--script</c> kind. It reuses the
/// spec sources and options in <c>.api2skill.json</c> and does not take a new spec
/// (<c>update</c> remains the spec-refresh command and keeps the recorded script kind).
/// </summary>
public static class MigrateCommand
{
    public static Command Create()
    {
        var pathArgument = new Argument<string>("skill-path")
        {
            Description = "Path to a previously generated skill directory (must contain .api2skill.json).",
        };
        var scriptOption = new Option<string>("--script")
        {
            Description = $"Script kind to switch to. Valid values: {ScriptEmitters.ValidKinds}.",
            Required = true,
        };

        var command = new Command("migrate", "Regenerate a skill with a different dispatcher language, preserving secrets, auth, and the token cache.")
        {
            pathArgument,
            scriptOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var skillPath = parseResult.GetValue(pathArgument)!;
            var scriptKind = parseResult.GetValue(scriptOption)!;
            return await RunAsync(skillPath, scriptKind, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(string skillPath, string scriptKind, CancellationToken cancellationToken)
    {
        var emitter = ScriptEmitters.TryCreate(scriptKind);
        if (emitter is null)
        {
            Console.Error.WriteLine($"Unknown --script '{scriptKind}'. Valid values: {ScriptEmitters.ValidKinds}.");
            return ExitCodes.UsageError;
        }

        var manifest = SkillManifestIo.TryLoad(skillPath);
        if (manifest is null)
        {
            Console.Error.WriteLine(
                $"'{skillPath}' does not look like an api2skill-generated skill — no readable {SkillManifestIo.FileName} found. " +
                "Use 'api2skill generate' to create it.");
            return ExitCodes.UsageError;
        }

        if (string.Equals(manifest.ScriptKind, emitter.Key, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Skill '{skillPath}' is already --script {emitter.Key}.");
            return ExitCodes.Success;
        }

        var sourceDir = Path.GetFullPath(skillPath);
        var parts = manifest.GetSpecSources().ToList();

        GenerateOptions options;
        if (parts.Count >= 2)
        {
            var generateParts = parts
                .Select(p => new GenerateSpecPart(p.Source, p.Id, p.Label))
                .ToList();
            options = new GenerateOptions(
                SpecSource: generateParts[0].Source,
                OutputDirectory: sourceDir,
                Name: manifest.Name,
                ScriptKind: emitter.Key,
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
            options = new GenerateOptions(
                SpecSource: parts[0].Source,
                OutputDirectory: sourceDir,
                Name: manifest.Name,
                ScriptKind: emitter.Key,
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

        return await GenerateCommand.RunAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
