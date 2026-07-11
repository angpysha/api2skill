namespace Api2Skill.Model;

public sealed record RequestBodyModel(
    string ContentType,
    bool Required,
    string? SchemaSummary);
