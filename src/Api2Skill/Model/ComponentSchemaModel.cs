namespace Api2Skill.Model;

/// <summary>
/// A named <c>components.schemas</c> entry persisted as raw OpenAPI JSON under
/// <c>reference/schemas/&lt;Name&gt;.json</c> (feature 011).
/// </summary>
public sealed record ComponentSchemaModel(string Name, string RawJson);
