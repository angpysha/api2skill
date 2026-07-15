namespace Api2Skill.Model;

/// <summary>One property (or array-items summary) for reference markdown.</summary>
public sealed record SchemaPropertyModel(
    string Name,
    string Type,
    bool Required,
    string? Description,
    string? Format,
    IReadOnlyList<string>? EnumValues = null);

/// <summary>One <c>oneOf</c>/<c>anyOf</c> branch shown in reference MD.</summary>
public sealed record SchemaVariantModel(
    int Index,
    string Summary,
    string? SchemaName = null);

/// <summary>
/// Structured schema detail for progressive-disclosure reference docs so callers know
/// exact request/response JSON shapes instead of guessing.
/// </summary>
public sealed record SchemaDetailModel(
    string? Summary,
    IReadOnlyList<SchemaPropertyModel> Properties,
    string? ExampleJson,
    IReadOnlyList<string>? EnumValues = null,
    string? SchemaName = null,
    bool Truncated = false,
    IReadOnlyList<SchemaVariantModel>? Variants = null);
