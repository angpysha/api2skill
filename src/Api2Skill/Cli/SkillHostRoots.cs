namespace Api2Skill.Cli;

/// <summary>
/// Supported project-scoped agent skill roots for <c>install-creator</c>
/// (specs/008-install-creator). Exactly four hosts — keep in sync with wiki/Install-Creator.md.
/// Personal/user home skill dirs are out of scope for v1.
/// </summary>
public static class SkillHostRoots
{
    /// <summary>Folder name under each skills root that holds the creator skill.</summary>
    public const string CreatorSkillFolder = "api2skill-creator";

    /// <summary>Creator skill entry file.</summary>
    public const string SkillFileName = "SKILL.md";

    /// <summary>
    /// The four supported project skill roots (FR-002).
    /// <list type="number">
    /// <item><description>Cursor → <c>.cursor/skills/</c></description></item>
    /// <item><description>Claude Code → <c>.claude/skills/</c></description></item>
    /// <item><description>GitHub Copilot → <c>.github/skills/</c></description></item>
    /// <item><description>Agentic / shared → <c>.agents/skills/</c></description></item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<SkillHostRoot> All { get; } =
    [
        // 1. Cursor — https://cursor.com/docs/skills
        new("Cursor", ".cursor/skills"),
        // 2. Claude Code — https://code.claude.com/docs/en/skills.md
        new("Claude Code", ".claude/skills"),
        // 3. GitHub Copilot — https://docs.github.com/en/copilot/concepts/agents/about-agent-skills
        new("GitHub Copilot", ".github/skills"),
        // 4. Agentic / shared (also loaded by Copilot + Cursor)
        new("Agentic", ".agents/skills"),
    ];
}

/// <param name="DisplayName">Human label shown in the interactive picker.</param>
/// <param name="RelativePath">Path relative to the current working directory (skills root).</param>
public sealed record SkillHostRoot(string DisplayName, string RelativePath);
