namespace Api2Skill.Cli;

/// <summary>Parsed <c>generate</c> options (contracts/cli.md).</summary>
public sealed record GenerateOptions(
    string SpecSource,
    string? OutputDirectory,
    string? Name,
    string ScriptKind,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> Exclude,
    bool Force,
    bool Insecure,
    string? Format,
    string? BaseUrl);
