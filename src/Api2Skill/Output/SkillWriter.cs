using Api2Skill.Emit;
using Api2Skill.Examples;
using Api2Skill.Model;

namespace Api2Skill.Output;

/// <summary>Thrown when the target directory exists and <c>--force</c> was not given (EC-10, FR-009).</summary>
public sealed class SkillDirectoryExistsException(string path)
    : Exception($"Output directory already exists: {path}. Pass --force to regenerate.");

/// <summary>
/// Orchestrates writing a <see cref="SkillModel"/> to disk: directory layout, the
/// exists/--force policy (preserving a real <c>secrets.json</c> across regeneration — FR-009,
/// NFR-1), and the shared content writers (SkillMdWriter, ReferenceWriter, SecretsScaffold)
/// plus the selected <see cref="IScriptEmitter"/>.
///
/// Generation happens entirely in a sibling staging directory, moved into place only once
/// every writer has succeeded (FR-010/EC-1's "no partial output" extended to <c>--force</c>
/// too — T039). Without this, a failure partway through a <c>--force</c> regeneration would
/// have already deleted the old skill dir (including any real, unrecoverable
/// <c>secrets.json</c>, which is only held in memory until the very end) and left an
/// incomplete directory in its place.
///
/// <c>--force</c> also preserves an existing <c>auth.json</c> (unless this run supplies a new
/// one via <c>--auth</c>/<c>--auth-config</c>), the entire <c>examples/</c> tree (specs/010),
/// and always preserves <c>.auth-cache.json</c> (+ its lock file), since that cache holds live
/// OAuth sessions (contracts/cli.md). The <c>.api2skill.json</c> generation manifest
/// (specs/003-skill-update-command) is always (re)written when supplied — it has no preservation
/// logic, since it should always reflect the most recent invocation.
///
/// <c>preserveFromDirectory</c> (specs/004-skill-rename-move-on-update) lets a caller preserve
/// credential/cache files from a directory other than <paramref name="outputDirectory"/> —
/// used when <c>update --out</c> relocates a skill and the old files live at the source path,
/// not the (possibly not-yet-existing) target path.
/// </summary>
public static class SkillWriter
{
    private const string AuthConfigFileName = "auth.json";

    public static DirectoryInfo Write(
        SkillModel model, string outputDirectory, bool force, IScriptEmitter emitter,
        string? authConfigJson = null, string? scaffoldAuthJson = null, string? manifestJson = null,
        string? preserveFromDirectory = null)
    {
        var targetDir = new DirectoryInfo(Path.GetFullPath(outputDirectory));
        byte[]? preservedSecrets = null;
        byte[]? preservedAuthConfig = null;
        byte[]? preservedTokenCache = null;
        byte[]? preservedTokenCacheLock = null;

        if (targetDir.Exists && !force)
        {
            throw new SkillDirectoryExistsException(outputDirectory);
        }

        // specs/004-skill-rename-move-on-update: when `update --out` relocates a skill,
        // credential/cache files must be read from the *old* directory even though the
        // *new* directory (targetDir) doesn't exist yet — preserveFromDirectory lets the
        // caller point preservation at that source directory instead of targetDir.
        var preserveSourceDir = preserveFromDirectory is { Length: > 0 }
            ? new DirectoryInfo(Path.GetFullPath(preserveFromDirectory))
            : targetDir;

        if (preserveSourceDir.Exists)
        {
            var secretsPath = Path.Combine(preserveSourceDir.FullName, SecretsScaffold.RealSecretsFileName);
            if (File.Exists(secretsPath))
            {
                preservedSecrets = File.ReadAllBytes(secretsPath);
            }

            var authConfigPath = Path.Combine(preserveSourceDir.FullName, AuthConfigFileName);
            if (authConfigJson is null && File.Exists(authConfigPath))
            {
                preservedAuthConfig = File.ReadAllBytes(authConfigPath);
            }

            var tokenCachePath = Path.Combine(preserveSourceDir.FullName, SecretsScaffold.TokenCacheFileName);
            if (File.Exists(tokenCachePath))
            {
                preservedTokenCache = File.ReadAllBytes(tokenCachePath);
            }

            var tokenCacheLockPath = tokenCachePath + ".lock";
            if (File.Exists(tokenCacheLockPath))
            {
                preservedTokenCacheLock = File.ReadAllBytes(tokenCacheLockPath);
            }
        }

        var stagingDir = new DirectoryInfo(Path.Combine(
            targetDir.Parent?.FullName ?? Directory.GetCurrentDirectory(),
            $".{targetDir.Name}.api2skill-staging-{Guid.NewGuid():N}"));
        stagingDir.Create();

        try
        {
            SkillMdWriter.Write(model, stagingDir, emitter);
            ReferenceWriter.Write(model, stagingDir);
            SecretsScaffold.Write(model, stagingDir);
            emitter.Emit(model, stagingDir);

            if (preservedSecrets is not null)
            {
                // Never parsed/embedded — copied back byte-for-byte, after every generated
                // file is already staged, so a real credential is never read during
                // generation itself.
                File.WriteAllBytes(Path.Combine(stagingDir.FullName, SecretsScaffold.RealSecretsFileName), preservedSecrets);
            }

            if (authConfigJson is not null)
            {
                File.WriteAllText(Path.Combine(stagingDir.FullName, AuthConfigFileName), authConfigJson);
            }
            else if (preservedAuthConfig is not null)
            {
                File.WriteAllBytes(Path.Combine(stagingDir.FullName, AuthConfigFileName), preservedAuthConfig);
            }
            else if (scaffoldAuthJson is not null)
            {
                File.WriteAllText(Path.Combine(stagingDir.FullName, AuthConfigFileName), scaffoldAuthJson);
            }

            if (preservedTokenCache is not null)
            {
                File.WriteAllBytes(Path.Combine(stagingDir.FullName, SecretsScaffold.TokenCacheFileName), preservedTokenCache);
            }
            if (preservedTokenCacheLock is not null)
            {
                File.WriteAllBytes(Path.Combine(stagingDir.FullName, SecretsScaffold.TokenCacheFileName + ".lock"), preservedTokenCacheLock);
            }

            if (manifestJson is not null)
            {
                // Always overwritten (unlike auth.json) — the manifest should always reflect
                // the most recent invocation's options (spec.md FR-007).
                File.WriteAllText(Path.Combine(stagingDir.FullName, SkillManifestIo.FileName), manifestJson);
            }

            // FR-003: preserve authored examples/ then re-link into regenerated reference MD.
            if (preserveSourceDir.Exists)
            {
                ExampleStore.CopyTree(preserveSourceDir.FullName, stagingDir.FullName);
            }

            var knownOps = model.Tags
                .SelectMany(t => t.Operations)
                .Select(o => o.OperationId)
                .ToHashSet(StringComparer.Ordinal);
            var orphans = ExampleReferenceLinker.SyncSkill(stagingDir.FullName, knownOps);
            foreach (var orphan in orphans)
            {
                Console.Error.WriteLine(
                    $"Warning: orphan example operationId '{orphan}' is not in the current skill (preserved under examples/).");
            }

            if (targetDir.Exists)
            {
                targetDir.Delete(recursive: true);
            }
            Directory.Move(stagingDir.FullName, targetDir.FullName);
        }
        catch
        {
            if (stagingDir.Exists)
            {
                stagingDir.Delete(recursive: true);
            }
            throw;
        }

        return targetDir;
    }
}
