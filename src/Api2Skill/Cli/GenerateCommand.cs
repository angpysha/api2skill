using System.CommandLine;
using System.Diagnostics;
using Api2Skill.Auth;
using Api2Skill.Emit;
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
    public const int AuthConfigError = 5;
}

/// <summary>
/// Builds the <c>generate</c> command (contracts/cli.md). Accepts a file, URL, or stdin
/// (<c>-</c>) spec source via <see cref="SpecSource.AcquireAsync"/>, and emits through
/// whichever of the three <see cref="IScriptEmitter"/> implementations <c>--script</c> selects
/// (<see cref="CsFileEmitter"/> default, <see cref="FsxEmitter"/>, <see cref="CsxEmitter"/>).
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
        var authConfigOption = new Option<string?>("--auth-config")
        {
            Description = "Path to an auth.json (explicit auth profiles) — contracts/auth-config.md. Mutually exclusive with --auth.",
        };
        var authOption = new Option<string?>("--auth")
        {
            Description = "Shorthand: scaffold a single global auth profile of type bearer|basic|custom. oauth2/entra need --auth-config. Mutually exclusive with --auth-config.",
        };
        var loginOption = new Option<bool>("--login")
        {
            Description = "After writing the skill, run interactive login for each authorization_code auth profile.",
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
            authConfigOption,
            authOption,
            loginOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new GenerateOptions(
                SpecSource: parseResult.GetValue(specArgument)!,
                OutputDirectory: parseResult.GetValue(outOption),
                Name: parseResult.GetValue(nameOption),
                ScriptKind: parseResult.GetValue(scriptOption)!,
                Include: SplitSelectors(parseResult.GetValue(includeOption)),
                Exclude: SplitSelectors(parseResult.GetValue(excludeOption)),
                Force: parseResult.GetValue(forceOption),
                Insecure: parseResult.GetValue(insecureOption),
                Format: parseResult.GetValue(formatOption),
                BaseUrl: parseResult.GetValue(baseUrlOption),
                AuthConfigPath: parseResult.GetValue(authConfigOption),
                AuthShorthand: parseResult.GetValue(authOption),
                Login: parseResult.GetValue(loginOption));

            return await RunAsync(options, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Expands comma-separated selector values (<c>--include tag:a,tag:b</c>) alongside
    /// repeatable-flag usage (<c>--include tag:a --include tag:b</c>) — contracts/cli.md
    /// promises both forms for <c>--include</c>/<c>--exclude</c>.
    /// </summary>
    internal static string[] SplitSelectors(string[]? raw) =>
        raw is null or []
            ? []
            : [.. raw.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))];

    /// <param name="preserveFromDirectory">
    /// specs/004-skill-rename-move-on-update: when set (by <c>UpdateCommand</c>'s relocate path),
    /// forwarded to <see cref="SkillWriter.Write"/> so credential/cache files are preserved from
    /// this directory instead of <c>options.OutputDirectory</c>. Not exposed as a CLI flag.
    /// </param>
    internal static async Task<int> RunAsync(GenerateOptions options, CancellationToken cancellationToken, string? preserveFromDirectory = null)
    {
        if (options.AuthConfigPath is { Length: > 0 } && options.AuthShorthand is { Length: > 0 })
        {
            Console.Error.WriteLine("--auth and --auth-config are mutually exclusive. Pass one or the other.");
            return ExitCodes.UsageError;
        }

        AuthConfig? authConfig = null;
        string? authConfigJson = null;
        if (options.AuthConfigPath is { Length: > 0 } authConfigPath)
        {
            try
            {
                authConfig = AuthConfigLoader.LoadFromFile(authConfigPath, out authConfigJson);
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.AcquisitionFailure;
            }
            catch (AuthConfigException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.AuthConfigError;
            }
        }
        else if (options.AuthShorthand is { Length: > 0 } authShorthand)
        {
            try
            {
                authConfig = AuthConfigLoader.CreateShorthand(authShorthand);
                authConfigJson = AuthConfigLoader.Serialize(authConfig);
            }
            catch (AuthShorthandUnsupportedException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.UsageError;
            }
        }

        MemoryStream stream;
        string format;
        try
        {
            (stream, format) = await SpecSource.AcquireAsync(options.SpecSource, options.Format, options.Insecure, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.AcquisitionFailure;
        }
        catch (HttpRequestException ex)
        {
            // Covers connection failures, DNS failures, and TLS certificate errors when
            // --insecure was not passed (EC-8).
            Console.Error.WriteLine($"Failed to fetch spec from {options.SpecSource}: {ex.Message}");
            if (!options.Insecure)
            {
                Console.Error.WriteLine("If the server uses a self-signed/untrusted certificate, pass --insecure (dev-only).");
            }
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
            BaseUrlOverride: options.BaseUrl,
            InsecureDefault: options.Insecure,
            AuthConfig: authConfig);

        SkillModel model;
        try
        {
            model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, buildOptions);
        }
        catch (AuthConfigCollisionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.AuthConfigError;
        }

        IScriptEmitter? emitter = options.ScriptKind switch
        {
            "cs" => new CsFileEmitter(),
            "fsx" => new FsxEmitter(),
            "csx" => new CsxEmitter(),
            _ => null,
        };
        if (emitter is null)
        {
            Console.Error.WriteLine($"Unknown --script '{options.ScriptKind}'. Valid values: cs, fsx, csx.");
            return ExitCodes.UsageError;
        }

        var outputDirectory = options.OutputDirectory is { Length: > 0 } o ? o : Path.Combine(".", name);

        // FR-001 (specs/003-skill-update-command): every generate records the options needed to
        // reproduce it later via `update`, without any secret/credential values.
        var manifestJson = SkillManifestIo.Serialize(new SkillManifest(
            Name: name,
            SpecSource: options.SpecSource,
            ScriptKind: options.ScriptKind,
            Include: options.Include,
            Exclude: options.Exclude,
            Format: options.Format,
            BaseUrl: options.BaseUrl,
            Insecure: options.Insecure));

        DirectoryInfo written;
        try
        {
            written = SkillWriter.Write(model, outputDirectory, options.Force, emitter, authConfigJson, manifestJson, preserveFromDirectory);
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

        if (options.Login && authConfig is not null)
        {
            await RunLoginForAuthorizationCodeProfilesAsync(authConfig, emitter, written, cancellationToken);
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// FR-017/T064: <c>--login</c> runs the interactive login once per <c>authorization_code</c>
    /// profile right after a successful write, priming <c>.auth-cache.json</c>. Generation stays
    /// fully non-interactive without this flag.
    /// </summary>
    private static async Task RunLoginForAuthorizationCodeProfilesAsync(
        AuthConfig authConfig, IScriptEmitter emitter, DirectoryInfo written, CancellationToken cancellationToken)
    {
        // RunnerDescription looks like "dotnet run scripts/call.cs --" / "dotnet fsi scripts/call.fsx --" /
        // "dotnet script scripts/call.csx --" — always "dotnet <rest>".
        var runnerArgs = emitter.RunnerDescription["dotnet ".Length..];

        foreach (var profile in authConfig.Profiles.Where(p => p.Type == AuthType.OAuth2 && p.OAuth!.Grant == OAuthGrant.AuthorizationCode))
        {
            Console.WriteLine($"--login: running interactive login for profile '{profile.Name}'...");
            var psi = new ProcessStartInfo("dotnet", $"{runnerArgs} login {profile.Name}")
            {
                WorkingDirectory = written.FullName,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi)!;
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine($"--login: login for profile '{profile.Name}' failed (exit {process.ExitCode}).");
            }
        }
    }
}
