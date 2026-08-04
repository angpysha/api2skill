using System.Text;

namespace Api2Skill.Emit;

/// <summary>
/// Filesystem-safe stems for <c>reference/&lt;tag&gt;.md</c>. OpenAPI tag names are free-form
/// and may contain <c>/</c> or other reserved path characters (FR-001 / bug 014).
/// </summary>
public static class ReferencePaths
{
    /// <summary>
    /// Builds a unique sanitized file stem for each distinct tag, preserving first-seen order.
    /// Display titles stay as the original tag; only the file stem is sanitized.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildTagFileStems(IEnumerable<string> tags)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            if (result.ContainsKey(tag))
            {
                continue;
            }

            var stem = SanitizeTagFileName(tag);
            if (string.IsNullOrEmpty(stem))
            {
                stem = "tag";
            }

            var candidate = stem;
            var n = 2;
            while (!claimed.Add(candidate))
            {
                candidate = $"{stem}-{n}";
                n++;
            }

            result[tag] = candidate;
        }

        return result;
    }

    /// <summary>
    /// Replaces path separators and other reserved filename characters with <c>-</c>,
    /// collapses runs, and trims edge dashes.
    /// </summary>
    public static string SanitizeTagFileName(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "tag";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(tag.Length);
        var lastWasDash = false;
        foreach (var c in tag.Trim())
        {
            var unsafeChar = c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'
                || Array.IndexOf(invalid, c) >= 0
                || char.IsControl(c);
            if (unsafeChar || char.IsWhiteSpace(c))
            {
                if (!lastWasDash)
                {
                    sb.Append('-');
                    lastWasDash = true;
                }

                continue;
            }

            sb.Append(c);
            lastWasDash = false;
        }

        var result = sb.ToString().Trim('-');
        return result.Length > 0 ? result : "tag";
    }
}
