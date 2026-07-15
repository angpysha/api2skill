namespace Api2Skill.Model;

/// <summary>Where a parameter is carried. Cookie/QueryString locations are out of MVP scope.</summary>
public enum ParameterLocation
{
    Path,
    Query,
    Header,
}

/// <summary>
/// Path/query/header parameter with enough OpenAPI schema detail for reference docs
/// (type, format, enum, examples, object/array shapes).
/// </summary>
public sealed record ParameterModel(
    string Name,
    ParameterLocation In,
    bool Required,
    string Type,
    string? Description,
    string? Format = null,
    IReadOnlyList<string>? EnumValues = null,
    string? Example = null,
    SchemaDetailModel? Schema = null);
