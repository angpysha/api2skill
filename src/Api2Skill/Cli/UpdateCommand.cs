using System.CommandLine;
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

        var command = new Command("update", "Regenerate a previously generated skill from a newer OpenAPI/Swagger document, reusing its saved generation options.")
        {
            pathArgument,
            specArgument,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var skillPath = parseResult.GetValue(pathArgument)!;
            var specSource = parseResult.GetValue(specArgument);
            return await RunAsync(skillPath, specSource, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(string skillPath, string? newSpecSource, CancellationToken cancellationToken)
    {
        var manifest = SkillManifestIo.TryLoad(skillPath);
        if (manifest is null)
        {
            Console.Error.WriteLine(
                $"'{skillPath}' does not look like an api2skill-generated skill — no readable {SkillManifestIo.FileName} found. " +
                "Use 'api2skill generate' to create it, or regenerate once with a current version of api2skill to add the manifest.");
            return ExitCodes.UsageError;
        }

        var options = new GenerateOptions(
            SpecSource: newSpecSource ?? manifest.SpecSource,
            OutputDirectory: skillPath,
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
            Login: false);

        return await GenerateCommand.RunAsync(options, cancellationToken).ConfigureAwait(false);
    }
}
