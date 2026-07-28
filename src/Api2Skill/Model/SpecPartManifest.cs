namespace Api2Skill.Model;

/// <summary>
/// One OpenAPI source within a composite skill (data-model.md SpecPartManifest).
/// Persisted in <c>.api2skill.json</c> → <c>specSources[]</c>.
/// </summary>
public sealed record SpecPartManifest(
    string Id,
    string Source,
    string? Label = null)
{
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Id : Label;
}

/// <summary>Compact row for the SKILL.md API parts table (D5 / FR-010).</summary>
public sealed record SpecPartSummary(
    string Id,
    string Label,
    string Source,
    int OperationCount);

/// <summary>How to resolve duplicate <c>operationId</c> values across parts (D3).</summary>
public enum OperationIdConflictMode
{
    Fail,
    Prefix,
}

/// <summary>Options for <see cref="SkillModelComposer.Compose"/>.</summary>
public sealed record CompositionOptions(
    OperationIdConflictMode OnOperationIdConflict = OperationIdConflictMode.Fail,
    string? BaseUrlOverride = null);
