using Api2Skill.Cli;

namespace Api2Skill.Tests.Cli;

public class InstallCreatorCommandTests : IDisposable
{
    private readonly string _workDir = Path.Combine(Path.GetTempPath(), "api2skill-install-creator-" + Guid.NewGuid().ToString("N"));
    private readonly string _templatePath;

    public InstallCreatorCommandTests()
    {
        Directory.CreateDirectory(_workDir);
        var templateDir = Path.Combine(_workDir, "bundled-templates", SkillHostRoots.CreatorSkillFolder);
        Directory.CreateDirectory(templateDir);
        _templatePath = Path.Combine(templateDir, SkillHostRoots.SkillFileName);
        File.WriteAllText(_templatePath, "# test creator skill\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void NonInteractive_WithoutTarget_ExitsUsageError()
    {
        var exit = InstallCreatorCommand.Run(
            target: null,
            force: false,
            isInteractive: false,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            stderr: TextWriter.Null);

        Assert.Equal(ExitCodes.UsageError, exit);
    }

    [Fact]
    public void Target_InstallsUnderApi2SkillCreatorFolder()
    {
        var skillsRoot = Path.Combine(_workDir, ".cursor", "skills");

        var exit = InstallCreatorCommand.Run(
            target: skillsRoot,
            force: false,
            isInteractive: false,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            stdout: TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exit);
        var dest = Path.Combine(skillsRoot, SkillHostRoots.CreatorSkillFolder, SkillHostRoots.SkillFileName);
        Assert.True(File.Exists(dest));
        Assert.Equal("# test creator skill\n", File.ReadAllText(dest));
    }

    [Fact]
    public void RelativeTarget_ResolvesAgainstWorkingDirectory()
    {
        var exit = InstallCreatorCommand.Run(
            target: ".github/skills",
            force: false,
            isInteractive: false,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            stdout: TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(Path.Combine(
            _workDir, ".github", "skills", SkillHostRoots.CreatorSkillFolder, SkillHostRoots.SkillFileName)));
    }

    [Fact]
    public void ExistingSkill_WithoutForce_NonInteractive_ExitsOutputExists()
    {
        var skillsRoot = Path.Combine(_workDir, ".claude", "skills");
        var destDir = Path.Combine(skillsRoot, SkillHostRoots.CreatorSkillFolder);
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, SkillHostRoots.SkillFileName);
        File.WriteAllText(dest, "original\n");

        var exit = InstallCreatorCommand.Run(
            target: skillsRoot,
            force: false,
            isInteractive: false,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            stdout: TextWriter.Null,
            stderr: TextWriter.Null);

        Assert.Equal(ExitCodes.OutputExists, exit);
        Assert.Equal("original\n", File.ReadAllText(dest));
    }

    [Fact]
    public void ExistingSkill_WithForce_Overwrites()
    {
        var skillsRoot = Path.Combine(_workDir, ".agents", "skills");
        var destDir = Path.Combine(skillsRoot, SkillHostRoots.CreatorSkillFolder);
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, SkillHostRoots.SkillFileName);
        File.WriteAllText(dest, "original\n");

        var exit = InstallCreatorCommand.Run(
            target: skillsRoot,
            force: true,
            isInteractive: false,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            stdout: TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Equal("# test creator skill\n", File.ReadAllText(dest));
    }

    [Fact]
    public void Interactive_ConfirmOverwrite_AllowsReplace()
    {
        var skillsRoot = Path.Combine(_workDir, ".cursor", "skills");
        var destDir = Path.Combine(skillsRoot, SkillHostRoots.CreatorSkillFolder);
        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, SkillHostRoots.SkillFileName);
        File.WriteAllText(dest, "original\n");

        var exit = InstallCreatorCommand.Run(
            target: skillsRoot,
            force: false,
            isInteractive: true,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            confirmOverwrite: _ => true,
            stdout: TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Equal("# test creator skill\n", File.ReadAllText(dest));
    }

    [Fact]
    public void PreselectedRoots_InstallsToMultipleHosts()
    {
        var exit = InstallCreatorCommand.Run(
            target: null,
            force: false,
            isInteractive: true,
            workingDirectory: _workDir,
            templatePath: _templatePath,
            preselectedRelativeRoots: [".cursor/skills", ".github/skills"],
            stdout: TextWriter.Null);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.True(File.Exists(Path.Combine(_workDir, ".cursor", "skills", SkillHostRoots.CreatorSkillFolder, SkillHostRoots.SkillFileName)));
        Assert.True(File.Exists(Path.Combine(_workDir, ".github", "skills", SkillHostRoots.CreatorSkillFolder, SkillHostRoots.SkillFileName)));
    }

    [Fact]
    public void MissingTemplate_ExitsAcquisitionFailure()
    {
        var exit = InstallCreatorCommand.Run(
            target: Path.Combine(_workDir, ".cursor", "skills"),
            force: false,
            isInteractive: false,
            workingDirectory: _workDir,
            templatePath: Path.Combine(_workDir, "does-not-exist.md"),
            stderr: TextWriter.Null);

        Assert.Equal(ExitCodes.AcquisitionFailure, exit);
    }

    [Fact]
    public void SkillHostRoots_ListsExactlyFourSupportedProjectRoots()
    {
        Assert.Equal(4, SkillHostRoots.All.Count);
        Assert.Equal([".cursor/skills", ".claude/skills", ".github/skills", ".agents/skills"],
            SkillHostRoots.All.Select(h => h.RelativePath).ToArray());
    }

    [Fact]
    public void CreatorTemplateLocator_FindsBundledContentAfterBuild()
    {
        var found = CreatorTemplateLocator.TryFind();
        Assert.NotNull(found);
        Assert.True(File.Exists(found));
        Assert.Contains("api2skill-creator", found, StringComparison.Ordinal);
        var text = File.ReadAllText(found);
        Assert.Contains("api2skill generate", text, StringComparison.Ordinal);
        Assert.Contains("--auth", text, StringComparison.Ordinal);
        Assert.Contains("callbackUrl", text, StringComparison.Ordinal);
    }
}
