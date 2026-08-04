using Api2Skill.Auth;
using Api2Skill.Emit;
using Api2Skill.Input;
using Api2Skill.Model;
using Api2Skill.Output;
using Api2Skill.Parsing;

namespace Api2Skill.Cli;

/// <summary>
/// Shared multi-part acquire → parse → build → compose → emit path (research R8/R13).
/// Per-part work runs in parallel via <see cref="Task.WhenAll"/>; compose and emit are serial.
/// Fail-fast: any part failure cancels siblings and writes no skill directory.
/// </summary>
public static class CompositeGenerateService
{
    public sealed record PartRequest(SpecPartManifest Manifest);

    public sealed record Result(
        DirectoryInfo Written,
        SkillModel Model,
        IReadOnlyList<string> Warnings);

    public static async Task<Result> GenerateAsync(
        string skillName,
        IReadOnlyList<PartRequest> parts,
        GenerateOptions options,
        AuthConfig? authConfig,
        string? authConfigJson,
        CancellationToken cancellationToken,
        string? preserveFromDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new CompositionException("At least one spec part is required.");
        }

        var ids = parts.Select(p => p.Manifest.Id).ToList();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            var dup = ids.GroupBy(x => x, StringComparer.Ordinal).First(g => g.Count() > 1).Key;
            throw new CompositionException($"Duplicate spec-id '{dup}' in one invocation.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var buildTasks = parts.Select(p => BuildPartAsync(p, options, authConfig, cts.Token)).ToArray();

        (SpecPartManifest Manifest, SkillModel Model, IReadOnlyList<string> LoadWarnings, byte[] SpecBytes, string Format)[] built;
        try
        {
            built = await Task.WhenAll(buildTasks).ConfigureAwait(false);
        }
        catch
        {
            await cts.CancelAsync().ConfigureAwait(false);
            throw;
        }

        // Preserve manifest order (WhenAll returns in task order matching parts).
        var conflictMode = string.Equals(options.OnConflict, "prefix", StringComparison.OrdinalIgnoreCase)
            ? OperationIdConflictMode.Prefix
            : OperationIdConflictMode.Fail;

        var composed = SkillModelComposer.Compose(
            skillName,
            built.Select(b => (b.Manifest, b.Model)).ToList(),
            new CompositionOptions(conflictMode, options.BaseUrl));

        string? scaffoldAuthJson = null;
        string? effectiveAuthJson = authConfigJson;
        if (authConfig is null && composed.SecuritySchemes.Count > 0)
        {
            var scaffold = AuthScaffold.Build(composed);
            scaffoldAuthJson = scaffold.Json;
            composed = composed with { AuthScaffoldGuidance = scaffold.Guidance };

            string? preservedAuthText = null;
            if (preserveFromDirectory is { Length: > 0 })
            {
                var preservedAuthPath = Path.Combine(preserveFromDirectory, "auth.json");
                if (File.Exists(preservedAuthPath))
                {
                    preservedAuthText = File.ReadAllText(preservedAuthPath);
                    effectiveAuthJson = AuthGuidanceOverlay.Apply(preservedAuthText, scaffold.Json);
                    scaffoldAuthJson = null;
                }
            }

            // Bake AuthProfileNames from the auth.json that will actually be written so
            // dispatcher profile lookups match on-disk profiles (merge first-wins vs scaffold).
            var bakeJson = effectiveAuthJson ?? scaffold.Json;
            if (TryParseAuthConfig(bakeJson, out var bakeConfig))
            {
                composed = SkillModelAuthApplier.Apply(composed, bakeConfig);
            }
        }
        else if (authConfig is not null && composed.SecuritySchemes.Count > 0)
        {
            effectiveAuthJson = AuthGuidanceOverlay.Apply(
                authConfigJson,
                AuthScaffold.Build(composed).Json);
            scaffoldAuthJson = null;
        }

        IScriptEmitter emitter = options.ScriptKind switch
        {
            "cs" => new CsFileEmitter(),
            "fsx" => new FsxEmitter(),
            "csx" => new CsxEmitter(),
            _ => throw new CompositionException(
                $"Unknown --script '{options.ScriptKind}'. Valid values: cs, fsx, csx."),
        };

        var outputDirectory = options.OutputDirectory is { Length: > 0 } o
            ? o
            : Path.Combine(".", skillName);

        var effectiveBaseUrl = options.BaseUrl ?? composed.BaseUrl;
        if (effectiveBaseUrl is not null
            && Uri.TryCreate(effectiveBaseUrl.Trim(), UriKind.Absolute, out var baseUri)
            && !baseUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            effectiveBaseUrl = composed.BaseUrl;
        }

        var manifestJson = SkillManifestIo.Serialize(new SkillManifest(
            Name: skillName,
            SpecSource: parts[0].Manifest.Source,
            ScriptKind: options.ScriptKind,
            Include: options.Include,
            Exclude: options.Exclude,
            Format: options.Format,
            BaseUrl: effectiveBaseUrl,
            Insecure: options.Insecure,
            SpecSources: parts.Select(p => p.Manifest).ToList()));

        var written = SkillWriter.Write(
            composed,
            outputDirectory,
            options.Force,
            emitter,
            effectiveAuthJson,
            scaffoldAuthJson,
            manifestJson,
            preserveFromDirectory);

        foreach (var part in built)
        {
            SpecCache.Write(written.FullName, part.Manifest.Id, part.Format, part.SpecBytes);
        }

        var warnings = built
            .SelectMany(b => b.LoadWarnings)
            .Concat(composed.Warnings)
            .ToList();

        return new Result(written, composed, warnings);
    }

    private static bool TryParseAuthConfig(string json, out AuthConfig config)
    {
        try
        {
            config = AuthConfigLoader.Parse(json);
            return config.Profiles.Count > 0;
        }
        catch (AuthConfigException)
        {
            config = null!;
            return false;
        }
    }

    private static async Task<(SpecPartManifest Manifest, SkillModel Model, IReadOnlyList<string> LoadWarnings, byte[] SpecBytes, string Format)>
        BuildPartAsync(
            PartRequest part,
            GenerateOptions options,
            AuthConfig? authConfig,
            CancellationToken cancellationToken)
    {
        MemoryStream stream;
        string format;
        try
        {
            (stream, format) = await SpecSource.AcquireAsync(
                    part.Manifest.Source,
                    options.Format,
                    options.Insecure,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Failed to fetch spec for part '{part.Manifest.Id}' from {part.Manifest.Source}: {ex.Message}",
                ex);
        }

        byte[] specBytes;
        LoadedSpec loaded;
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
                throw new OpenApiParseException(
                    ex.Errors.Select(e => new ParseError(
                        e.Message,
                        e.Pointer is { Length: > 0 }
                            ? $"[{part.Manifest.Id}] {e.Pointer}"
                            : $"[{part.Manifest.Id}]")).ToList());
            }
        }

        var buildOptions = new BuildOptions(
            Name: options.Name is { Length: > 0 } ? Slug.Create(options.Name) : part.Manifest.Id,
            IncludeSelectors: options.Include,
            ExcludeSelectors: options.Exclude,
            BaseUrlOverride: options.BaseUrl,
            InsecureDefault: options.Insecure,
            AuthConfig: authConfig);

        var model = SkillModelBuilder.Build(loaded.Document, loaded.SpecVersion, buildOptions);
        return (part.Manifest, model, loaded.Warnings, specBytes, format);
    }
}
