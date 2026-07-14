namespace Api2Skill.Model;

public sealed record ResponseModel(
    string StatusCode,
    string? Description,
    string? ContentType = null,
    string? SchemaSummary = null,
    SchemaDetailModel? Schema = null);
