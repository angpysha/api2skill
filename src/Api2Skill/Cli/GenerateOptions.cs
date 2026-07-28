using Api2Skill.Model;

namespace Api2Skill.Cli;

/// <summary>One <c>--spec</c> occurrence with optional id/label (contracts/cli.md).</summary>
public sealed record GenerateSpecPart(string Source, string? Id = null, string? Label = null);

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
    string? BaseUrl,
    string? AuthConfigPath = null,
    string? AuthShorthand = null,
    bool Login = false,
    IReadOnlyList<GenerateSpecPart>? SpecParts = null,
    string OnConflict = "fail");
