using System.Text;
using System.Text.RegularExpressions;

namespace Api2Skill.Examples;

/// <summary>
/// Builds and patches the <c>**Authored examples**</c> markdown table in <c>reference/&lt;tag&gt;.md</c>
/// (data-model.md LinkBlock). Links are re-derived from the filesystem on every sync.
/// </summary>
public static partial class ExampleReferenceLinker
{
    public const string SectionHeading = "**Authored examples**";

    private const string Caption =
        "Prefer these payloads when calling/testing this operation; do not invent JSON when a request example exists. On failure: ask the user before updating examples or contracts.";

    /// <summary>
    /// Scan <c>examples/</c> and rewrite Authored examples sections in all <c>reference/*.md</c>.
    /// Returns orphan operationIds (not in <paramref name="knownOperationIds"/> when provided).
    /// </summary>
    public static IReadOnlyList<string> SyncSkill(
        string skillDirectory,
        IReadOnlySet<string>? knownOperationIds = null)
    {
        var discovery = ExampleStore.Discover(skillDirectory, knownOperationIds);
        var byOp = discovery.Items
            .GroupBy(i => i.OperationId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ExampleArtifact>)g.ToList(), StringComparer.Ordinal);

        var referenceDir = Path.Combine(skillDirectory, "reference");
        if (!Directory.Exists(referenceDir))
        {
            return discovery.Orphans;
        }

        foreach (var mdPath in Directory.EnumerateFiles(referenceDir, "*.md"))
        {
            var text = File.ReadAllText(mdPath);
            var updated = SyncDocument(text, byOp);
            if (!string.Equals(text, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(mdPath, updated);
            }
        }

        return discovery.Orphans;
    }

    /// <summary>Inject or replace Authored examples blocks under matching <c>## operationId</c> headings.</summary>
    public static string SyncDocument(
        string markdown,
        IReadOnlyDictionary<string, IReadOnlyList<ExampleArtifact>> examplesByOperationId)
    {
        var matches = OperationHeadingLine().Matches(markdown);
        if (matches.Count == 0)
        {
            return StripAuthoredSection(markdown);
        }

        var sb = new StringBuilder();
        var lastIndex = 0;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var sectionStart = match.Index;
            var sectionEnd = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;

            // Prefix before this ## heading (strip stray authored blocks outside ops).
            if (sectionStart > lastIndex)
            {
                sb.Append(StripAuthoredSection(markdown[lastIndex..sectionStart]));
            }

            var heading = match.Value;
            var operationId = match.Groups[1].Value.Trim();
            var bodyStart = match.Index + match.Length;
            var body = markdown[bodyStart..sectionEnd];
            var cleanedBody = StripAuthoredSection(body);

            sb.Append(heading);

            if (examplesByOperationId.TryGetValue(operationId, out var artifacts) && artifacts.Count > 0)
            {
                sb.Append(InsertBeforeSeparator(cleanedBody, BuildSection(operationId, artifacts)));
            }
            else
            {
                sb.Append(cleanedBody);
            }

            lastIndex = sectionEnd;
        }

        if (lastIndex < markdown.Length)
        {
            sb.Append(StripAuthoredSection(markdown[lastIndex..]));
        }

        return sb.ToString();
    }

    public static string BuildSection(string operationId, IReadOnlyList<ExampleArtifact> artifacts)
    {
        var sb = new StringBuilder();
        sb.AppendLine(SectionHeading);
        sb.AppendLine();
        sb.AppendLine(Caption);
        sb.AppendLine();
        sb.AppendLine("| name | request | response |");
        sb.AppendLine("|------|---------|----------|");
        foreach (var item in artifacts.OrderBy(a => a.Name, StringComparer.Ordinal))
        {
            var requestCell = item.HasRequest
                ? $"[{ExamplePaths.RequestFileName}]({ExamplePaths.RelativeLinkFromReference(operationId, item.Name, ExamplePaths.RequestFileName)})"
                : "—";
            var responseCell = item.HasResponse
                ? $"[{ExamplePaths.ResponseFileName}]({ExamplePaths.RelativeLinkFromReference(operationId, item.Name, ExamplePaths.ResponseFileName)})"
                : "—";
            sb.AppendLine($"| `{item.Name}` | {requestCell} | {responseCell} |");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>Collect <c>## operationId</c> headings from all reference markdown files.</summary>
    public static IReadOnlyDictionary<string, string> IndexOperationsByTagFile(string skillDirectory)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var referenceDir = Path.Combine(skillDirectory, "reference");
        if (!Directory.Exists(referenceDir))
        {
            return result;
        }

        foreach (var mdPath in Directory.EnumerateFiles(referenceDir, "*.md"))
        {
            foreach (Match m in OperationHeadingLine().Matches(File.ReadAllText(mdPath)))
            {
                var opId = m.Groups[1].Value.Trim();
                result.TryAdd(opId, mdPath);
            }
        }

        return result;
    }

    public static IReadOnlySet<string> KnownOperationIds(string skillDirectory) =>
        IndexOperationsByTagFile(skillDirectory).Keys.ToHashSet(StringComparer.Ordinal);

    private static string InsertBeforeSeparator(string body, string block)
    {
        // Prefer inserting before the operation's trailing "---" horizontal rule.
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal);
        const string sep = "\n---\n";
        var sepIndex = normalized.IndexOf(sep, StringComparison.Ordinal);

        if (sepIndex < 0)
        {
            if (normalized.StartsWith("---\n", StringComparison.Ordinal) || normalized == "---")
            {
                return block + normalized;
            }

            var prefix = normalized.Length == 0 || normalized.EndsWith("\n\n", StringComparison.Ordinal)
                ? normalized
                : normalized.TrimEnd('\n') + "\n\n";
            return prefix + block;
        }

        var before = normalized[..sepIndex].TrimEnd('\n') + "\n\n";
        var after = normalized[(sepIndex + 1)..]; // "---\n..."
        return before + block + after;
    }

    private static string StripAuthoredSection(string text) =>
        AuthoredSectionRegex().Replace(text, string.Empty);

    [GeneratedRegex(@"^## (.+)$", RegexOptions.Multiline)]
    private static partial Regex OperationHeadingLine();

    [GeneratedRegex(
        @"\*\*Authored examples\*\*[ \t]*\r?\n(?:.*\r?\n)*?(?=\r?\n---|\r?\n## |\z)",
        RegexOptions.CultureInvariant)]
    private static partial Regex AuthoredSectionRegex();
}
