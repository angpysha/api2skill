using System.CommandLine;
using Api2Skill.Input;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Cli;

/// <summary>Exit codes per contracts/cli.md.</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int ParseFailure = 1;
    public const int UsageError = 2;
    public const int OutputExists = 3;
    public const int AcquisitionFailure = 4;
}

/// <summary>
/// Builds the <c>generate</c> command (contracts/cli.md). Foundational (T005) wires the file
/// input path end-to-end; URL/stdin acquisition (T029, US3) and the three script emitters
/// (T017/T033/T034, US1/US4) plug into this same command later without changing its shape —
/// SkillWriter already accepts an <see cref="Emit.IScriptEmitter"/>, this command just doesn't
/// have one to pass yet.
/// </summary>
public static class GenerateCommand
{
    public static Command Create()
    {
        var specArgument = new Argument<string>("spec-source")
        {
            Description = "Local file path, remote URL, or '-' for stdin.",
        };
        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Output directory. Defaults to ./<slug-of-title>.",
        };
        var nameOption = new Option<string?>("--name")
        {
            Description = "Override the skill name (defaults to a slug of info.title).",
        };
        var scriptOption = new Option<string>("--script")
        {
            Description = "Emitter to use: cs (default), fsx, or csx.",
            DefaultValueFactory = _ => "cs",
        };
        var includeOption = new Option<string[]>("--include")
        {
            Description = "Keep only matching tag:/path:/op: selectors (repeatable).",
            DefaultValueFactory = _ => [],
        };
        var excludeOption = new Option<string[]>("--exclude")
        {
            Description = "Drop matching tag:/path:/op: selectors (repeatable, applied after --include).",
            DefaultValueFactory = _ => [],
        };
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Regenerate over an existing output directory.",
        };
        var insecureOption = new Option<bool>("--insecure")
        {
            Description = "Dev-only: accept untrusted HTTPS certificates when fetching a spec URL or making generated calls.",
        };
        var formatOption = new Option<string?>("--format")
        {
            Description = "Force the input format (json|yaml) instead of sniffing it.",
        };
        var baseUrlOption = new Option<string?>("--base-url")
        {
            Description = "Base URL to use when the spec has no `servers` entry.",
        };

        var command = new Command("generate", "Generate a Claude Skill from an OpenAPI/Swagger document")
        {
            specArgument,
            outOption,
            nameOption,
            scriptOption,
            includeOption,
            excludeOption,
            forceOption,
            insecureOption,
            formatOption,
            baseUrlOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new GenerateOptions(
                SpecSource: parseResult.GetValue(specArgument)!,
                OutputDirectory: parseResult.GetValue(outOption),
                Name: parseResult.GetValue(nameOption),
                ScriptKind: parseResult.GetValue(scriptOption)!,
                Include: parseResult.GetValue(includeOption) ?? [],
                Exclude: parseResult.GetValue(excludeOption) ?? [],
                Force: parseResult.GetValue(forceOption),
                Insecure: parseResult.GetValue(insecureOption),
                Format: parseResult.GetValue(formatOption),
                BaseUrl: parseResult.GetValue(baseUrlOption));

            return await RunAsync(options, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    internal static async Task<int> RunAsync(GenerateOptions options, CancellationToken cancellationToken)
    {
        MemoryStream stream;
        string format;
        try
        {
            // Foundational wires the file source only; URL ("http.../https://...") and stdin
            // ("-") arrive in US3 (T029). Fail clearly rather than mis-report a generic
            // file-not-found for a source kind that simply isn't implemented yet.
            if (options.SpecSource == "-" || options.SpecSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || options.SpecSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("URL and stdin spec sources are not implemented yet (planned: US3).");
                return ExitCodes.AcquisitionFailure;
            }

            (stream, format) = await SpecSource.AcquireFileAsync(options.SpecSource, options.Format, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.AcquisitionFailure;
        }

        LoadedSpec loaded;
        await using (stream)
        {
            try
            {
                loaded = await OpenApiLoader.LoadAsync(stream, format, cancellationToken).ConfigureAwait(false);
            }
            catch (OpenApiParseException ex)
            {
                Console.Error.WriteLine("Failed to parse the OpenAPI document:");
                foreach (var error in ex.Errors)
                {
                    Console.Error.WriteLine(error.Pointer is { Length: > 0 }
                        ? $"  {error.Pointer}: {error.Message}"
                        : $"  {error.Message}");
                }
                return ExitCodes.ParseFailure;
            }
        }

        var name = options.Name is { Length: > 0 }
            ? Slug.Create(options.Name)
            : Slug.Create(loaded.Document.Info?.Title is { Length: > 0 } title ? title : "skill");

        var buildOptions = new BuildOptions(
            Name: name,
            IncludeSelectors: options.Include,
            ExcludeSelectors: options.Exclude,
            BaseUrlOverride: options.BaseUrl);

        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, buildOptions);

        var outputDirectory = options.OutputDirectory is { Length: > 0 } o ? o : Path.Combine(".", name);

        DirectoryInfo written;
        try
        {
            // No emitter is wired up yet (Foundational) — this creates the directory only.
            // SKILL.md/reference/dispatcher content lands with the emitters in US1/US4.
            written = SkillWriter.Write(model, outputDirectory, options.Force, emitter: null);
        }
        catch (SkillDirectoryExistsException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.OutputExists;
        }

        foreach (var warning in loaded.Warnings.Concat(model.Warnings))
        {
            Console.Error.WriteLine($"warning: {warning}");
        }

        var opCount = model.Tags.Sum(t => t.Operations.Count);
        Console.WriteLine($"{written.FullName} ({opCount} operation(s), {model.Tags.Count} tag(s))");
        return ExitCodes.Success;
    }
}
