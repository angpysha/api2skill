namespace Api2Skill.Emit;

/// <summary>
/// Resolves <c>--script</c> to an <see cref="IScriptEmitter"/>. One switch so generate,
/// composite generate, and migrate stay in agreement.
/// </summary>
public static class ScriptEmitters
{
    public const string ValidKinds = "cs, fsx, csx, py";

    public static IScriptEmitter? TryCreate(string? kind)
    {
        return kind?.Trim().ToLowerInvariant() switch
        {
            "cs" => new CsFileEmitter(),
            "fsx" => new FsxEmitter(),
            "csx" => new CsxEmitter(),
            "py" => new PyEmitter(),
            _ => null,
        };
    }
}
