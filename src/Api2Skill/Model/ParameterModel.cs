namespace Api2Skill.Model;

/// <summary>Where a parameter is carried. Cookie/QueryString locations are out of MVP scope.</summary>
public enum ParameterLocation
{
    Path,
    Query,
    Header,
}

public sealed record ParameterModel(
    string Name,
    ParameterLocation In,
    bool Required,
    string Type,
    string? Description);
