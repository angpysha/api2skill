using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api2Skill.Cli;

/// <summary>
/// Tracks the bundled vs installed <c>api2skill-creator</c> skill version via
/// <c>.creator-version.json</c> next to <c>SKILL.md</c> (specs/012-multi-spec-skill).
/// </summary>
public static class CreatorSkillVersion
{
    public const string VersionFileName = ".creator-version.json";

    /// <summary>Tool package version — source of truth for the bundled creator skill.</summary>
    public static Version BundledVersion =>
        typeof(CreatorSkillVersion).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string BundledVersionString() => Format(BundledVersion);

    public static Version? TryGetInstalledVersion(string creatorSkillDirectory)
    {
        var path = Path.Combine(creatorSkillDirectory, VersionFileName);
        if (!File.Exists(path))
        {
            // Legacy install before version tracking — treat as oldest.
            var skillMd = Path.Combine(creatorSkillDirectory, SkillHostRoots.SkillFileName);
            return File.Exists(skillMd) ? new Version(0, 0, 0) : null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("version", out var versionEl)
                && versionEl.GetString() is { Length: > 0 } text
                && Version.TryParse(text, out var version))
            {
                return version;
            }
        }
        catch (JsonException)
        {
            return new Version(0, 0, 0);
        }

        return new Version(0, 0, 0);
    }

    public static void WriteInstalledVersion(string creatorSkillDirectory, Version version)
    {
        var root = new JsonObject
        {
            ["version"] = Format(version),
        };
        File.WriteAllText(
            Path.Combine(creatorSkillDirectory, VersionFileName),
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool IsUpdateAvailable(string creatorSkillDirectory)
    {
        var installed = TryGetInstalledVersion(creatorSkillDirectory);
        // Normalize so Assembly 0.6.4.0 equals parsed "0.6.4" (Revision -1).
        return installed is not null && Normalize(BundledVersion) > Normalize(installed);
    }

    /// <summary>Three-part package version for stable comparison and display.</summary>
    internal static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0));

    public static string Format(Version version)
    {
        var n = Normalize(version);
        return $"{n.Major}.{n.Minor}.{n.Build}";
    }

    public static string DescribeUpdate(Version installed) =>
        $"Update available: api2skill-creator {Format(installed)} → {BundledVersionString()}";
}
