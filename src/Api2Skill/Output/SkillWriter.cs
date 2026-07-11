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
/// </summary>
public static class SkillWriter
{
    public static DirectoryInfo Write(SkillModel model, string outputDirectory, bool force, IScriptEmitter emitter)
    {
        var dir = new DirectoryInfo(outputDirectory);
        byte[]? preservedSecrets = null;

        if (dir.Exists)
        {
            if (!force)
            {
                throw new SkillDirectoryExistsException(outputDirectory);
            }

            var secretsPath = Path.Combine(dir.FullName, SecretsScaffold.RealSecretsFileName);
            if (File.Exists(secretsPath))
            {
                preservedSecrets = File.ReadAllBytes(secretsPath);
            }

            dir.Delete(recursive: true);
        }

        dir.Create();

        SkillMdWriter.Write(model, dir, emitter);
        ReferenceWriter.Write(model, dir);
        SecretsScaffold.Write(model, dir);
        emitter.Emit(model, dir);

        if (preservedSecrets is not null)
        {
            // Never parsed/embedded — copied back byte-for-byte, after every generated file is
            // already written, so a real credential is never read during generation itself.
            File.WriteAllBytes(Path.Combine(dir.FullName, SecretsScaffold.RealSecretsFileName), preservedSecrets);
        }

        return dir;
    }
}
