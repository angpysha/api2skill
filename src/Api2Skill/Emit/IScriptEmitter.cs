using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// The pluggable-emitter seam (Constitution III / FR-006). Every generated-script kind
/// (the MVP's <c>.cs</c>/<c>.fsx</c>/<c>.csx</c>, and any future kind — bash, Python, a
/// compiled client) implements this against <see cref="SkillModel"/> only, never against
/// Microsoft.OpenApi types. Writes the dispatcher script (<c>scripts/call.&lt;ext&gt;</c>,
/// contracts/skill-output.md) into <paramref name="skillDirectory"/>.
/// </summary>
public interface IScriptEmitter
{
    /// <summary>Short key used by <c>--script</c> (e.g. "cs", "fsx", "csx").</summary>
    string Key { get; }

    /// <summary>File extension of the dispatcher script, without the leading dot.</summary>
    string FileExtension { get; }

    /// <summary>Human-readable runner instructions for SKILL.md (FR-006b), e.g. "dotnet run scripts/call.cs --".</summary>
    string RunnerDescription { get; }

    void Emit(SkillModel model, DirectoryInfo skillDirectory);
}
