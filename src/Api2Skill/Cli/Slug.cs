using System.Text.RegularExpressions;

namespace Api2Skill.Cli;

/// <summary>Derives the default skill name from `info.title` (FR-008).</summary>
public static partial class Slug
{
    public static string Create(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        // `+` already collapses runs of non-alphanumeric characters into a single '-'.
        var replaced = NonAlphaNumeric().Replace(lowered, "-").Trim('-');
        return replaced.Length > 0 ? replaced : "skill";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphaNumeric();
}
