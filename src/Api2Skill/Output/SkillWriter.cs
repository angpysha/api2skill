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
/// </summary>
public static class SkillWriter
{
    public static DirectoryInfo Write(SkillModel model, string outputDirectory, bool force, IScriptEmitter emitter)
    {
        var targetDir = new DirectoryInfo(Path.GetFullPath(outputDirectory));
        byte[]? preservedSecrets = null;

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
