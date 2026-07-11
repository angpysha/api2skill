using Api2Skill.Emit;
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
/// one via <c>--auth</c>/<c>--auth-config</c>) and always preserves <c>.auth-cache.json</c>
/// (+ its lock file), since that cache holds live OAuth sessions (contracts/cli.md).
/// </summary>
public static class SkillWriter
{
    private const string AuthConfigFileName = "auth.json";

    public static DirectoryInfo Write(
        SkillModel model, string outputDirectory, bool force, IScriptEmitter emitter, string? authConfigJson = null)
    {
        var targetDir = new DirectoryInfo(Path.GetFullPath(outputDirectory));
        byte[]? preservedSecrets = null;
        byte[]? preservedAuthConfig = null;
        byte[]? preservedTokenCache = null;
        byte[]? preservedTokenCacheLock = null;

        if (targetDir.Exists)
        {
            if (!force)
            {
                throw new SkillDirectoryExistsException(outputDirectory);
            }

            var secretsPath = Path.Combine(targetDir.FullName, SecretsScaffold.RealSecretsFileName);
            if (File.Exists(secretsPath))
            {
                preservedSecrets = File.ReadAllBytes(secretsPath);
            }

            var authConfigPath = Path.Combine(targetDir.FullName, AuthConfigFileName);
            if (authConfigJson is null && File.Exists(authConfigPath))
            {
                preservedAuthConfig = File.ReadAllBytes(authConfigPath);
            }

            var tokenCachePath = Path.Combine(targetDir.FullName, SecretsScaffold.TokenCacheFileName);
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

            if (preservedTokenCache is not null)
            {
                File.WriteAllBytes(Path.Combine(stagingDir.FullName, SecretsScaffold.TokenCacheFileName), preservedTokenCache);
            }
            if (preservedTokenCacheLock is not null)
            {
                File.WriteAllBytes(Path.Combine(stagingDir.FullName, SecretsScaffold.TokenCacheFileName + ".lock"), preservedTokenCacheLock);
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
