using Api2Skill.Cli;

namespace Api2Skill.Tests.Cli;

public class CreatorSkillVersionTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "api2skill-creator-version-" + Guid.NewGuid().ToString("N"));

    public CreatorSkillVersionTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteInstalledVersion_RoundTrips()
    {
        CreatorSkillVersion.WriteInstalledVersion(_dir, new Version(1, 2, 3));
        var loaded = CreatorSkillVersion.TryGetInstalledVersion(_dir);
        Assert.NotNull(loaded);
        Assert.Equal(new Version(1, 2, 3), loaded);
    }

    [Fact]
    public void LegacySkillWithoutVersionFile_TreatedAsZero()
    {
        File.WriteAllText(Path.Combine(_dir, SkillHostRoots.SkillFileName), "# skill\n");
        var loaded = CreatorSkillVersion.TryGetInstalledVersion(_dir);
        Assert.Equal(new Version(0, 0, 0), loaded);
        Assert.True(CreatorSkillVersion.IsUpdateAvailable(_dir));
    }

    [Fact]
    public void MissingSkill_ReturnsNull()
    {
        Assert.Null(CreatorSkillVersion.TryGetInstalledVersion(_dir));
    }
}

public class CreatorCheckUpdateCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(
        Path.GetTempPath(),
        "api2skill-creator-check-" + Guid.NewGuid().ToString("N"));

    public CreatorCheckUpdateCommandTests() => Directory.CreateDirectory(_workDir);

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NoInstallations_ExitsSuccess()
    {
        var sw = new StringWriter();
        var exit = CreatorCommand.CheckUpdateCommand.Run(_workDir, sw, TextWriter.Null);
        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("not installed", sw.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UpToDateInstallation_ExitsSuccess()
    {
        InstallCreator(".cursor/skills", CreatorSkillVersion.BundledVersion);

        var sw = new StringWriter();
        var exit = CreatorCommand.CheckUpdateCommand.Run(_workDir, sw, TextWriter.Null);
        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("up to date", sw.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OlderInstallation_ExitsUpdateAvailable()
    {
        InstallCreator(".cursor/skills", new Version(0, 1, 0));

        var sw = new StringWriter();
        var stderr = new StringWriter();
        var exit = CreatorCommand.CheckUpdateCommand.Run(_workDir, sw, stderr);
        Assert.Equal(CreatorCommand.UpdateAvailableExitCode, exit);
        Assert.Contains("update available", sw.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ask the user", stderr.ToString(), StringComparison.Ordinal);
    }

    private void InstallCreator(string relativeRoot, Version installedVersion)
    {
        var destDir = Path.Combine(_workDir, relativeRoot, SkillHostRoots.CreatorSkillFolder);
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, SkillHostRoots.SkillFileName), "# creator\n");
        CreatorSkillVersion.WriteInstalledVersion(destDir, installedVersion);
    }
}
