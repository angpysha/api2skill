using Api2Skill.Emit;
using Api2Skill.Model;

namespace Api2Skill.Output;

/// <summary>Thrown when the target directory exists and <c>--force</c> was not given (EC-10, FR-009).</summary>
public sealed class SkillDirectoryExistsException(string path)
    : Exception($"Output directory already exists: {path}. Pass --force to regenerate.");

/// <summary>
/// Orchestrates writing a <see cref="SkillModel"/> to disk: directory layout, the
/// exists/--force policy, and the shared content writers (SkillMdWriter, ReferenceWriter)
/// plus the selected <see cref="IScriptEmitter"/>. <c>SecretsScaffold</c> and the
/// secrets-preserving nuance of <c>--force</c> (never clobber a real secrets.json) land in
/// US2 (T023/T025) — for now <c>force: true</c> simply clears and recreates the directory.
/// </summary>
public static class SkillWriter
{
    public static DirectoryInfo Write(SkillModel model, string outputDirectory, bool force, IScriptEmitter emitter)
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

        SkillMdWriter.Write(model, dir, emitter);
        ReferenceWriter.Write(model, dir);
        emitter.Emit(model, dir);

        return dir;
    }
}
