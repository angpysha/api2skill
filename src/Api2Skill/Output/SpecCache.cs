using Api2Skill.Cli;

namespace Api2Skill.Output;

/// <summary>
/// Persists acquired OpenAPI documents under <c>.api2skill/specs/</c> so <c>merge</c> can
/// recompose without re-fetching live <c>specSource</c> URLs (FR-002 / bug 014).
/// </summary>
public static class SpecCache
{
    public const string RootDirectoryName = ".api2skill";
    public const string SpecsDirectoryName = "specs";

    public static string SpecsDirectory(string skillDirectory) =>
        Path.Combine(skillDirectory, RootDirectoryName, SpecsDirectoryName);

    public static string CacheFilePath(string skillDirectory, string specId, string format)
    {
        var ext = string.Equals(format, "yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "yml", StringComparison.OrdinalIgnoreCase)
            ? "yaml"
            : "json";
        var safeId = Slug.Create(specId);
        return Path.Combine(SpecsDirectory(skillDirectory), $"{safeId}.{ext}");
    }

    /// <summary>Writes raw spec bytes; overwrites any previous cache for that part id.</summary>
    public static string Write(string skillDirectory, string specId, string format, ReadOnlySpan<byte> content)
    {
        var dir = SpecsDirectory(skillDirectory);
        Directory.CreateDirectory(dir);
        var path = CacheFilePath(skillDirectory, specId, format);
        File.WriteAllBytes(path, content.ToArray());
        return path;
    }

    /// <summary>
    /// Returns the preferred generate source for merge: local cache when present and non-empty,
    /// otherwise the original <paramref name="specSource"/>.
    /// </summary>
    public static string ResolveMergeSource(string skillDirectory, string specId, string specSource)
    {
        foreach (var ext in new[] { "json", "yaml" })
        {
            var path = CacheFilePath(skillDirectory, specId, ext);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return path;
            }
        }

        return specSource;
    }
}
