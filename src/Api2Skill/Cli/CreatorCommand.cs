using System.CommandLine;

namespace Api2Skill.Cli;

/// <summary>
/// <c>creator</c> subcommands for the bundled <c>api2skill-creator</c> agent skill
/// (specs/012-multi-spec-skill — version check / self-update guidance).
/// </summary>
public static class CreatorCommand
{
    /// <summary>Exit when at least one installed copy is older than the bundled skill.</summary>
    public const int UpdateAvailableExitCode = 8;

    public static Command Create()
    {
        var command = new Command("creator", "Bundled api2skill-creator skill utilities.");
        command.Add(CheckUpdateCommand.Create());
        return command;
    }

    public static class CheckUpdateCommand
    {
        public static Command Create()
        {
            var workingDirOption = new Option<string?>("--working-dir")
            {
                Description = "Repository root to scan (default: current directory).",
            };

            var command = new Command(
                "check-update",
                "Report installed api2skill-creator versions under standard skill roots.")
            {
                workingDirOption,
            };

            command.SetAction((parseResult, _) =>
            {
                var workingDir = parseResult.GetValue(workingDirOption);
                return Task.FromResult(Run(workingDir, Console.Out, Console.Error));
            });

            return command;
        }

        internal static int Run(
            string? workingDirectory,
            TextWriter stdout,
            TextWriter stderr,
            IReadOnlyList<SkillHostRoot>? hosts = null)
        {
            var cwd = string.IsNullOrWhiteSpace(workingDirectory)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(workingDirectory);

            var bundled = CreatorSkillVersion.BundledVersionString();
            stdout.WriteLine($"api2skill-creator version check (bundled {bundled})");
            stdout.WriteLine();

            var anyInstalled = false;
            var anyUpdate = false;

            foreach (var host in hosts ?? SkillHostRoots.All)
            {
                var creatorDir = Path.Combine(cwd, host.RelativePath, SkillHostRoots.CreatorSkillFolder);
                var skillFile = Path.Combine(creatorDir, SkillHostRoots.SkillFileName);
                var label = $"{host.RelativePath}/{SkillHostRoots.CreatorSkillFolder}";

                if (!File.Exists(skillFile))
                {
                    stdout.WriteLine($"  {label,-42} not installed");
                    continue;
                }

                anyInstalled = true;
                var installed = CreatorSkillVersion.TryGetInstalledVersion(creatorDir)!;
                var installedText = CreatorSkillVersion.Format(installed);

                if (CreatorSkillVersion.IsUpdateAvailable(creatorDir))
                {
                    anyUpdate = true;
                    stdout.WriteLine(
                        $"  {label,-42} installed {installedText}  → update available (bundled {bundled})");
                }
                else
                {
                    stdout.WriteLine($"  {label,-42} installed {installedText}  (up to date)");
                }
            }

            stdout.WriteLine();
            if (!anyInstalled)
            {
                stdout.WriteLine(
                    "No api2skill-creator installation found under standard skill roots. " +
                    "Install with: api2skill install-creator --target .cursor/skills");
                return ExitCodes.Success;
            }

            if (anyUpdate)
            {
                stderr.WriteLine(
                    "A newer api2skill-creator is bundled with this tool. " +
                    "Ask the user before updating. Example: api2skill install-creator --target .cursor/skills");
                return UpdateAvailableExitCode;
            }

            stdout.WriteLine("All installed copies are up to date.");
            return ExitCodes.Success;
        }
    }
}
