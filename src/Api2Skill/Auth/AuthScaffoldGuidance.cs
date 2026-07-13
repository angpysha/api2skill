using Api2Skill.Model;

namespace Api2Skill.Auth;

/// <summary>Whether a referenced scheme received an active scaffold profile (data-model.md §1).</summary>
public enum SchemeScaffoldStatus
{
    Scaffolded,
    ManualOnly,
}

/// <summary>One scheme row in scaffold naming guidance and SKILL.md (data-model.md §1).</summary>
public sealed record SchemeGuidanceEntry(
    string SchemeId,
    string SuggestedProfileName,
    SchemeScaffoldStatus Status,
    SecuritySchemeKind Kind,
    IReadOnlyList<string> OperationIds,
    IReadOnlyList<string> Tags);

/// <summary>Per-tag copy-paste profile examples for tag-scoped attach (not active at runtime).</summary>
public sealed record TagAttachExample(
    string Tag,
    IReadOnlyList<string> SchemeIds,
    IReadOnlyList<AuthProfile> ExampleProfiles);

/// <summary>Structured naming guidance attached to an auto-scaffold run (data-model.md §1).</summary>
public sealed record AuthScaffoldGuidance(
    IReadOnlyList<SchemeGuidanceEntry> Schemes,
    IReadOnlyList<TagAttachExample> TagAttachExamples);

/// <summary>Inactive auth.json template plus guidance for SKILL.md (data-model.md §1).</summary>
public sealed record AuthScaffoldResult(
    string Json,
    AuthScaffoldGuidance Guidance);
