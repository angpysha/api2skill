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
        return installed is not null && BundledVersion > installed;
    }

    public static string Format(Version version) =>
        $"{version.Major}.{version.Minor}.{version.Build}";

    public static string DescribeUpdate(Version installed) =>
        $"Update available: api2skill-creator {Format(installed)} → {BundledVersionString()}";
}
