using Api2Skill.Emit;
using Api2Skill.Model;

namespace Api2Skill.Output;

/// <summary>Thrown when the target directory exists and <c>--force</c> was not given (EC-10, FR-009).</summary>
public sealed class SkillDirectoryExistsException(string path)
    : Exception($"Output directory already exists: {path}. Pass --force to regenerate.");

/// <summary>
/// Orchestrates writing a <see cref="SkillModel"/> to disk: directory layout and the
/// exists/--force policy. Foundational (T011) only wires directory creation — the actual
/// content writers (SkillMdWriter, ReferenceWriter, SecretsScaffold) and the emitter are
/// plugged in starting with US1 (T015-T017); the secrets-preserving nuance of <c>--force</c>
/// (never clobber a real secrets.json) is completed in US2 (T025). For now <c>force: true</c>
/// simply clears and recreates the directory.
/// </summary>
public static class SkillWriter
{
    public static DirectoryInfo Write(SkillModel model, string outputDirectory, bool force, IScriptEmitter? emitter)
    {
        var dir = new DirectoryInfo(outputDirectory);
        if (dir.Exists)
        {
            if (!force)
            {
                throw new SkillDirectoryExistsException(outputDirectory);
            }

            dir.Delete(recursive: true);
        }

        dir.Create();

        emitter?.Emit(model, dir);

        return dir;
    }
}
