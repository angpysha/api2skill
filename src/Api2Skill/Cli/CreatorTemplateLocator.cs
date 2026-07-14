namespace Api2Skill.Cli;

/// <summary>
/// Resolves the bundled <c>api2skill-creator/SKILL.md</c> template shipped with the tool
/// package (Content + CopyToOutputDirectory + Pack in the csproj). Prefers
/// <see cref="AppContext.BaseDirectory"/>, then the executing assembly directory.
/// </summary>
public static class CreatorTemplateLocator
{
    private static readonly string[] RelativeSegments =
        ["templates", SkillHostRoots.CreatorSkillFolder, SkillHostRoots.SkillFileName];

    /// <summary>Returns the full path to the bundled SKILL.md, or null if not found.</summary>
    public static string? TryFind()
    {
        foreach (var root in CandidateRoots())
        {
            var path = Path.GetFullPath(Path.Combine([root, .. RelativeSegments]));
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;

        var location = typeof(CreatorTemplateLocator).Assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            var dir = Path.GetDirectoryName(location);
            if (!string.IsNullOrEmpty(dir))
            {
                yield return dir;
            }
        }
    }
}
