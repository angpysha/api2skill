using System.Text.RegularExpressions;

namespace Api2Skill.Examples;

/// <summary>
/// Filesystem-safe path helpers for <c>examples/&lt;operationId&gt;/&lt;name&gt;/request|response.json</c>
/// (contracts/examples-layout.md, data-model.md).
/// </summary>
public static partial class ExamplePaths
{
    public const string RootDirectoryName = "examples";
    public const string DefaultName = "default";
    public const string RequestFileName = "request.json";
    public const string ResponseFileName = "response.json";

    /// <summary>
    /// Filesystem-safe single path segment (operationId or name): no separators, no <c>.</c>/<c>..</c>.
    /// Allows camelCase operationIds such as <c>addPet</c>.
    /// </summary>
    public static bool IsSafePathSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed is "." or ".." || trimmed.Contains('/') || trimmed.Contains('\\') || trimmed.Contains(':'))
        {
            return false;
        }

        return SafeSegmentRegex().IsMatch(trimmed);
    }

    /// <summary>Example name slug: <c>[a-z0-9]([a-z0-9-]*[a-z0-9])?</c>.</summary>
    public static bool IsValidName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return NameSlugRegex().IsMatch(value.Trim());
    }

    public static string NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim();

    public static string ExamplesRoot(string skillDirectory) =>
        Path.Combine(skillDirectory, RootDirectoryName);

    public static string ExampleDirectory(string skillDirectory, string operationId, string name) =>
        Path.Combine(ExamplesRoot(skillDirectory), operationId, NormalizeName(name));

    public static string RequestPath(string skillDirectory, string operationId, string name) =>
        Path.Combine(ExampleDirectory(skillDirectory, operationId, name), RequestFileName);

    public static string ResponsePath(string skillDirectory, string operationId, string name) =>
        Path.Combine(ExampleDirectory(skillDirectory, operationId, name), ResponseFileName);

    /// <summary>Relative path from <c>reference/&lt;tag&gt;.md</c> to an example file.</summary>
    public static string RelativeLinkFromReference(string operationId, string name, string fileName) =>
        $"../{RootDirectoryName}/{operationId}/{NormalizeName(name)}/{fileName}";

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSegmentRegex();

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex NameSlugRegex();
}
