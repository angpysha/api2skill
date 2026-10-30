using System.CommandLine;
using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Input;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Cli;

/// <summary>Exit codes per contracts/cli.md (generate 0–5; oauth-capture/login add 6–7).</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int ParseFailure = 1;
    public const int UsageError = 2;
    public const int OutputExists = 3;
    public const int AcquisitionFailure = 4;
    public const int AuthConfigError = 5;

    /// <summary>Capture timeout / hosted unreachable / protocol not registered (<c>oauth-capture</c>).</summary>
    public const int CaptureTimeout = 6;

    /// <summary>OAuth error returned on redirect (<c>error=</c> query).</summary>
    public const int OAuthRedirectError = 7;
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
        var specArgument = new Argument<string?>("spec-source")
        {
            Description = "Local file path, remote URL, or '-' for stdin. Optional when --spec is used.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var specOption = new Option<string[]>("--spec")
        {
            Description = "OpenAPI source (repeatable). When present, positional spec-source is optional.",
            DefaultValueFactory = _ => [],
        };
        var specIdOption = new Option<string[]>("--spec-id")
        {
            Description = "Id for the corresponding --spec (same order). Defaults to slug of source.",
            DefaultValueFactory = _ => [],
        };
        var specLabelOption = new Option<string[]>("--spec-label")
        {
            Description = "Display label for the corresponding --spec (same order). Defaults to id.",
            DefaultValueFactory = _ => [],
        };
        var onConflictOption = new Option<string>("--on-conflict")
        {
            Description = "operationId collision across parts: fail (default) or prefix.",
            DefaultValueFactory = _ => "fail",
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
            Description = $"Emitter to use: {ScriptEmitters.ValidKinds}. Default: cs.",
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
            specOption,
            specIdOption,
            specLabelOption,
            onConflictOption,
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
            var positional = parseResult.GetValue(specArgument);
            var specs = parseResult.GetValue(specOption) ?? [];
            var ids = parseResult.GetValue(specIdOption) ?? [];
            var labels = parseResult.GetValue(specLabelOption) ?? [];
            var parts = BuildSpecParts(positional, specs, ids, labels);

            var options = new GenerateOptions(
                SpecSource: parts.Count > 0 ? parts[0].Source : positional ?? "",
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
                Login: parseResult.GetValue(loginOption),
                SpecParts: parts,
                OnConflict: parseResult.GetValue(onConflictOption)!);

            return await RunAsync(options, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Resolves positional + repeatable <c>--spec</c>/<c>--spec-id</c>/<c>--spec-label</c>
    /// into ordered parts. When any <c>--spec</c> is present, positional is ignored.
    /// </summary>
    internal static IReadOnlyList<GenerateSpecPart> BuildSpecParts(
        string? positional,
        string[] specs,
        string[] ids,
        string[] labels)
    {
        if (specs.Length > 0)
        {
            var list = new List<GenerateSpecPart>(specs.Length);
            for (var i = 0; i < specs.Length; i++)
            {
                list.Add(new GenerateSpecPart(
                    specs[i],
                    Id: i < ids.Length ? ids[i] : null,
                    Label: i < labels.Length ? labels[i] : null));
            }

            return list;
        }

        if (positional is { Length: > 0 })
        {
            return [new GenerateSpecPart(positional)];
        }

        return [];
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

        if (!string.Equals(options.OnConflict, "fail", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.OnConflict, "prefix", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown --on-conflict '{options.OnConflict}'. Valid values: fail, prefix.");
            return ExitCodes.UsageError;
        }

        var rawParts = options.SpecParts is { Count: > 0 }
            ? options.SpecParts
            : options.SpecSource is { Length: > 0 }
                ? [new GenerateSpecPart(options.SpecSource)]
                : Array.Empty<GenerateSpecPart>();

        if (rawParts.Count == 0)
        {
            Console.Error.WriteLine("Missing spec source. Pass a positional <spec-source> or one or more --spec options.");
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

        if (rawParts.Count >= 2)
        {
            return await RunCompositeAsync(options, rawParts, authConfig, authConfigJson, cancellationToken, preserveFromDirectory)
                .ConfigureAwait(false);
        }

        // Single-spec path (unchanged behavior for existing callers / goldens).
        options = options with { SpecSource = rawParts[0].Source };
        return await RunSingleAsync(options, authConfig, authConfigJson, cancellationToken, preserveFromDirectory)
            .ConfigureAwait(false);
    }

    private static async Task<int> RunCompositeAsync(
        GenerateOptions options,
        IReadOnlyList<GenerateSpecPart> rawParts,
        AuthConfig? authConfig,
        string? authConfigJson,
        CancellationToken cancellationToken,
        string? preserveFromDirectory)
    {
        List<CompositeGenerateService.PartRequest> requests;
        try
        {
            requests = rawParts.Select(ToPartRequest).ToList();
        }
        catch (CompositionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.UsageError;
        }

        var skillName = options.Name is { Length: > 0 }
            ? Slug.Create(options.Name)
            : Slug.Create(string.Join("-", requests.Select(r => r.Manifest.Id)));

        try
        {
            var result = await CompositeGenerateService.GenerateAsync(
                    skillName,
                    requests,
                    options,
                    authConfig,
                    authConfigJson,
                    cancellationToken,
                    preserveFromDirectory)
                .ConfigureAwait(false);

            foreach (var warning in result.Warnings)
            {
                Console.Error.WriteLine($"warning: {warning}");
            }

            var opCount = result.Model.Tags.Sum(t => t.Operations.Count);
            Console.WriteLine($"{result.Written.FullName} ({opCount} operation(s), {result.Model.Tags.Count} tag(s), {requests.Count} spec part(s))");

            if (options.Login && authConfig is not null)
            {
                await RunLoginForAuthorizationCodeProfilesAsync(authConfig, result.Written, cancellationToken);
            }

            return ExitCodes.Success;
        }
        catch (CompositionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.UsageError;
        }
        catch (SkillDirectoryExistsException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.OutputExists;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.AcquisitionFailure;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine(ex.Message);
            if (!options.Insecure)
            {
                Console.Error.WriteLine("If the server uses a self-signed/untrusted certificate, pass --insecure (dev-only).");
            }

            return ExitCodes.AcquisitionFailure;
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
        catch (AuthConfigCollisionException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.AuthConfigError;
        }
    }

    internal static CompositeGenerateService.PartRequest ToPartRequest(GenerateSpecPart part)
    {
        var id = part.Id is { Length: > 0 }
            ? Slug.Create(part.Id)
            : SkillManifest.DeriveLegacyPartId("part", part.Source);
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new CompositionException($"Could not derive a spec-id for source '{part.Source}'.");
        }

        var label = part.Label is { Length: > 0 } ? part.Label : id;
        return new CompositeGenerateService.PartRequest(new SpecPartManifest(id, part.Source, label));
    }

    private static async Task<int> RunSingleAsync(
        GenerateOptions options,
        AuthConfig? authConfig,
        string? authConfigJson,
        CancellationToken cancellationToken,
        string? preserveFromDirectory)
    {
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
        byte[] specBytes;
        await using (stream)
        {
            specBytes = stream.ToArray();
            stream.Position = 0;
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

        string? scaffoldAuthJson = null;
        if (authConfig is null && model.SecuritySchemes.Count > 0)
        {
            var scaffold = AuthScaffold.Build(model);
            scaffoldAuthJson = scaffold.Json;
            model = model with { AuthScaffoldGuidance = scaffold.Guidance };
            try
            {
                var scaffoldConfig = AuthConfigLoader.Parse(scaffold.Json);
                if (scaffoldConfig.Profiles.Count > 0)
                {
                    model = SkillModelAuthApplier.Apply(model, scaffoldConfig);
                }
            }
            catch (AuthConfigException)
            {
                // Scaffold may be guidance-only (manual schemes); leave AuthProfileNames empty.
            }
        }
        else if (authConfig is not null && model.SecuritySchemes.Count > 0)
        {
            authConfigJson = AuthGuidanceOverlay.Apply(authConfigJson, AuthScaffold.Build(model).Json);
        }

        var emitter = ScriptEmitters.TryCreate(options.ScriptKind);
        if (emitter is null)
        {
            Console.Error.WriteLine($"Unknown --script '{options.ScriptKind}'. Valid values: {ScriptEmitters.ValidKinds}.");
            return ExitCodes.UsageError;
        }

        var outputDirectory = options.OutputDirectory is { Length: > 0 } o ? o : Path.Combine(".", name);

        // FR-001 (specs/003-skill-update-command): every generate records the options needed to
        // reproduce it later via `update`, without any secret/credential values.
        // Always emit v2 specSources[] (DD2); single-part synthesizes one entry.
        var partId = SkillManifest.DeriveLegacyPartId(name, options.SpecSource);
        var manifestJson = SkillManifestIo.Serialize(new SkillManifest(
            Name: name,
            SpecSource: options.SpecSource,
            ScriptKind: options.ScriptKind,
            Include: options.Include,
            Exclude: options.Exclude,
            Format: options.Format,
            BaseUrl: options.BaseUrl,
            Insecure: options.Insecure,
            SpecSources: [new SpecPartManifest(partId, options.SpecSource, partId)]));

        DirectoryInfo written;
        try
        {
            written = SkillWriter.Write(
                model, outputDirectory, options.Force, emitter, authConfigJson, scaffoldAuthJson, manifestJson,
                preserveFromDirectory);
        }
        catch (SkillDirectoryExistsException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return ExitCodes.OutputExists;
        }

        SpecCache.Write(written.FullName, partId, format, specBytes);

        foreach (var warning in loaded.Warnings.Concat(model.Warnings))
        {
            Console.Error.WriteLine($"warning: {warning}");
        }

        var opCount = model.Tags.Sum(t => t.Operations.Count);
        Console.WriteLine($"{written.FullName} ({opCount} operation(s), {model.Tags.Count} tag(s))");

        if (options.Login && authConfig is not null)
        {
            await RunLoginForAuthorizationCodeProfilesAsync(authConfig, written, cancellationToken);
        }

        return ExitCodes.Success;
    }

    /// <summary>
    /// FR-017/T064: <c>--login</c> runs the interactive login once per <c>authorization_code</c>
    /// profile right after a successful write, priming <c>.auth-cache.json</c>. Generation stays
    /// fully non-interactive without this flag.
    /// </summary>
    private static async Task RunLoginForAuthorizationCodeProfilesAsync(
        AuthConfig authConfig, DirectoryInfo written, CancellationToken cancellationToken)
    {
        foreach (var profile in authConfig.Profiles.Where(p => p.Type == AuthType.OAuth2 && p.OAuth!.Grant == OAuthGrant.AuthorizationCode))
        {
            Console.WriteLine($"--login: running interactive login for profile '{profile.Name}'...");
            var exit = await SkillOAuthLogin.RunAsync(
                new SkillOAuthLogin.RunOptions(SkillDir: written.FullName, ProfileName: profile.Name),
                stdout: Console.Out,
                stderr: Console.Error,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (exit != ExitCodes.Success)
            {
                Console.Error.WriteLine($"--login: login for profile '{profile.Name}' failed (exit {exit}).");
            }
        }
    }
}
