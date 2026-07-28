using System.CommandLine;

namespace Api2Skill.Cli;

/// <summary>
/// <c>install-creator</c> (specs/008-install-creator): copies the bundled
/// <c>api2skill-creator</c> skill into one or more project skill roots so an agent can
/// interview the user and assemble correct <c>api2skill generate|update</c> commands.
/// </summary>
public static class InstallCreatorCommand
{
    public static Command Create()
    {
        var targetOption = new Option<string?>("--target")
        {
            Description =
                "Skills root directory that will contain api2skill-creator/ " +
                "(e.g. .cursor/skills). Required when stdin is not a TTY.",
        };
        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Overwrite an existing api2skill-creator/SKILL.md without prompting.",
        };

        var command = new Command(
            "install-creator",
            "Install the api2skill-creator agent skill into project skill root(s).")
        {
            targetOption,
            forceOption,
        };

        command.SetAction((parseResult, _) =>
        {
            var target = parseResult.GetValue(targetOption);
            var force = parseResult.GetValue(forceOption);
            return Task.FromResult(Run(target, force));
        });

        return command;
    }

    /// <summary>
    /// Installs the creator skill. Test hooks override TTY detection, selection, overwrite
    /// confirm, working directory, and template path so unit tests need no real Console.
    /// </summary>
    internal static int Run(
        string? target,
        bool force,
        bool? isInteractive = null,
        string? workingDirectory = null,
        string? templatePath = null,
        IReadOnlyList<string>? preselectedRelativeRoots = null,
        Func<string, string, bool>? confirmOverwrite = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;
        var cwd = workingDirectory ?? Directory.GetCurrentDirectory();
        var interactive = isInteractive ?? (!Console.IsInputRedirected && !Console.IsOutputRedirected);

        var template = templatePath ?? CreatorTemplateLocator.TryFind();
        if (template is null || !File.Exists(template))
        {
            stderr.WriteLine(
                "Bundled templates/api2skill-creator/SKILL.md was not found next to the tool. " +
                "Reinstall api2skill or run from a build that copies Content templates.");
            return ExitCodes.AcquisitionFailure;
        }

        IReadOnlyList<string> roots;
        if (!string.IsNullOrWhiteSpace(target))
        {
            roots = [Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(cwd, target))];
        }
        else if (preselectedRelativeRoots is not null)
        {
            roots = preselectedRelativeRoots
                .Select(r => Path.GetFullPath(Path.Combine(cwd, r)))
                .ToArray();
        }
        else if (!interactive)
        {
            stderr.WriteLine(
                "install-creator requires --target when stdin/stdout is not a TTY " +
                "(e.g. api2skill install-creator --target .cursor/skills).");
            return ExitCodes.UsageError;
        }
        else
        {
            var labels = SkillHostRoots.All
                .Select(h => $"{h.DisplayName}  ({h.RelativePath}/)")
                .ToArray();
            stdout.WriteLine();
            var selected = ConsoleMultiSelect.Run(labels, stdout, () => Console.ReadKey(intercept: true));
            if (selected.Count == 0)
            {
                stderr.WriteLine("No skill roots selected. Nothing installed.");
                return ExitCodes.UsageError;
            }

            roots = selected
                .Select(i => Path.GetFullPath(Path.Combine(cwd, SkillHostRoots.All[i].RelativePath)))
                .ToArray();
        }

        var anyFailure = false;
        var anySkippedExists = false;
        var successCount = 0;

        foreach (var skillsRoot in roots)
        {
            var destDir = Path.Combine(skillsRoot, SkillHostRoots.CreatorSkillFolder);
            var destFile = Path.Combine(destDir, SkillHostRoots.SkillFileName);

            try
            {
                if (File.Exists(destFile))
                {
                    var allowOverwrite = force;
                    if (!allowOverwrite)
                    {
                        if (interactive)
                        {
                            var confirm = confirmOverwrite ?? DefaultConfirmOverwrite;
                            allowOverwrite = confirm(destFile, destDir);
                        }
                    }

                    if (!allowOverwrite)
                    {
                        var installed = CreatorSkillVersion.TryGetInstalledVersion(destDir);
                        var hint = installed is not null && CreatorSkillVersion.IsUpdateAvailable(destDir)
                            ? $" {CreatorSkillVersion.DescribeUpdate(installed)}."
                            : string.Empty;
                        stderr.WriteLine(
                            $"Refusing to overwrite existing '{destFile}'.{hint} Re-run with --force " +
                            (interactive ? "or confirm when prompted." : "after explicit approval."));
                        anySkippedExists = true;
                        anyFailure = true;
                        continue;
                    }
                }

                Directory.CreateDirectory(destDir);
                File.Copy(template, destFile, overwrite: true);
                CreatorSkillVersion.WriteInstalledVersion(destDir, CreatorSkillVersion.BundledVersion);
                stdout.WriteLine(
                    $"Installed api2skill-creator {CreatorSkillVersion.BundledVersionString()} → {destFile}");
                successCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                stderr.WriteLine($"Failed to install into '{skillsRoot}': {ex.Message}");
                anyFailure = true;
            }
        }

        if (!anyFailure)
        {
            return ExitCodes.Success;
        }

        if (successCount == 0 && anySkippedExists)
        {
            return ExitCodes.OutputExists;
        }

        return ExitCodes.UsageError;
    }

    private static bool DefaultConfirmOverwrite(string destFile, string destDir)
    {
        var installed = CreatorSkillVersion.TryGetInstalledVersion(destDir);
        if (installed is not null && CreatorSkillVersion.IsUpdateAvailable(destDir))
        {
            Console.Write($"{CreatorSkillVersion.DescribeUpdate(installed)} Install update? [y/N] ");
        }
        else if (installed is not null)
        {
            Console.Write(
                $"api2skill-creator {CreatorSkillVersion.Format(installed)} is already installed at '{destFile}'. " +
                "Overwrite anyway? [y/N] ");
        }
        else
        {
            Console.Write($"Overwrite existing '{destFile}'? [y/N] ");
        }

        var line = Console.ReadLine();
        return line is not null && line.Trim().Equals("y", StringComparison.OrdinalIgnoreCase);
    }
}
