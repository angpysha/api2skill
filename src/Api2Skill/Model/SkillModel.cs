using Api2Skill.Auth;

namespace Api2Skill.Model;

/// <summary>
/// Which OpenAPI/Swagger version the source document was detected as (data-model.md).
/// Mirrors Microsoft.OpenApi's OpenApiSpecVersion so downstream code (emitters) never
/// takes a dependency on the Microsoft.OpenApi package (Constitution III).
/// </summary>
public enum SpecVersionKind
{
    OpenApi2_0,
    OpenApi3_0,
    OpenApi3_1,
    OpenApi3_2,
}

/// <summary>
/// The emitter-agnostic root model produced by <see cref="SkillModelBuilder"/> and consumed
/// by every <see cref="IScriptEmitter"/> plus the shared SKILL.md/reference/secrets writers.
/// This is the contract boundary between Parse and Emit (Constitution III).
/// </summary>
public sealed record SkillModel(
    string Name,
    string Title,
    string? Description,
    string? Version,
    string? BaseUrl,
    SpecVersionKind SpecVersion,
    IReadOnlyList<SecuritySchemeModel> SecuritySchemes,
    IReadOnlyList<TagGroup> Tags,
    IReadOnlyList<string> Warnings,
    bool InsecureDefault = false,
    AuthConfig? AuthConfig = null,
    AuthScaffoldGuidance? AuthScaffoldGuidance = null);

/// <summary>A tag-grouped bucket of operations for the SKILL.md operation index (D6).</summary>
public sealed record TagGroup(
    string Tag,
    string? Summary,
    IReadOnlyList<OperationModel> Operations);
