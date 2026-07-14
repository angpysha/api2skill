using System.CommandLine;
using System.Text.Json;
using Api2Skill.Examples;

namespace Api2Skill.Cli;

/// <summary>
/// <c>api2skill example add|list|remove|sync</c> — manage authored endpoint examples
/// (contracts/cli-example.md).
/// </summary>
public static class ExampleCommand
{
    public static Command Create()
    {
        var command = new Command("example", "Add, list, remove, or sync authored request/response examples")
        {
            CreateAdd(),
            CreateList(),
            CreateRemove(),
            CreateSync(),
        };
        return command;
    }

    private static Command CreateAdd()
    {
        var skillOption = SkillOption();
        var opOption = new Option<string>("--op")
        {
            Description = "operationId to attach the example to.",
            Required = true,
        };
        var nameOption = new Option<string>("--name")
        {
            Description = "Example name slug (default: default).",
            DefaultValueFactory = _ => ExamplePaths.DefaultName,
        };
        var requestOption = new Option<string?>("--request")
        {
            Description = "Path to request JSON, or '-' for stdin.",
        };
        var responseOption = new Option<string?>("--response")
        {
            Description = "Path to response JSON, or '-' for stdin.",
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing example files.",
            DefaultValueFactory = _ => false,
        };

        var add = new Command("add", "Write an authored example and link it from reference/<tag>.md")
        {
            skillOption, opOption, nameOption, requestOption, responseOption, forceOption,
        };

        add.SetAction((parseResult, _) =>
        {
            var exit = RunAdd(
                skillDir: parseResult.GetValue(skillOption),
                operationId: parseResult.GetValue(opOption),
                name: parseResult.GetValue(nameOption),
                requestSource: parseResult.GetValue(requestOption),
                responseSource: parseResult.GetValue(responseOption),
                force: parseResult.GetValue(forceOption),
                stdout: Console.Out,
                stderr: Console.Error);
            return Task.FromResult(exit);
        });

        return add;
    }

    private static Command CreateList()
    {
        var skillOption = SkillOption();
        var opOption = new Option<string?>("--op")
        {
            Description = "Filter by operationId.",
        };

        var list = new Command("list", "List authored examples in a skill")
        {
            skillOption, opOption,
        };

        list.SetAction((parseResult, _) =>
        {
            var exit = RunList(
                skillDir: parseResult.GetValue(skillOption),
                operationId: parseResult.GetValue(opOption),
                stdout: Console.Out,
                stderr: Console.Error);
            return Task.FromResult(exit);
        });

        return list;
    }

    private static Command CreateRemove()
    {
        var skillOption = SkillOption();
        var opOption = new Option<string>("--op")
        {
            Description = "operationId.",
            Required = true,
        };
        var nameOption = new Option<string>("--name")
        {
            Description = "Example name slug.",
            Required = true,
        };

        var remove = new Command("remove", "Delete a named example and re-sync reference links")
        {
            skillOption, opOption, nameOption,
        };

        remove.SetAction((parseResult, _) =>
        {
            var exit = RunRemove(
                skillDir: parseResult.GetValue(skillOption),
                operationId: parseResult.GetValue(opOption),
                name: parseResult.GetValue(nameOption),
                stdout: Console.Out,
                stderr: Console.Error);
            return Task.FromResult(exit);
        });

        return remove;
    }

    private static Command CreateSync()
    {
        var skillOption = SkillOption();
        var sync = new Command("sync", "Re-link all examples into reference/*.md")
        {
            skillOption,
        };

        sync.SetAction((parseResult, _) =>
        {
            var exit = RunSync(
                skillDir: parseResult.GetValue(skillOption),
                stdout: Console.Out,
                stderr: Console.Error);
            return Task.FromResult(exit);
        });

        return sync;
    }

    private static Option<string> SkillOption() => new("--skill")
    {
        Description = "Skill root directory.",
        Required = true,
    };

    /// <summary>Testable <c>example add</c>.</summary>
    public static int RunAdd(
        string? skillDir,
        string? operationId,
        string? name,
        string? requestSource,
        string? responseSource,
        bool force,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (!TryResolveSkillDir(skillDir, stderr, out var skill))
        {
            return ExitCodes.AcquisitionFailure;
        }

        if (string.IsNullOrWhiteSpace(operationId))
        {
            ConsoleColorWriter.WriteError("--op is required.", stderr);
            return ExitCodes.UsageError;
        }

        var op = operationId.Trim();
        var exampleName = ExamplePaths.NormalizeName(name);

        if (!ExamplePaths.IsSafePathSegment(op))
        {
            ConsoleColorWriter.WriteError($"Invalid --op segment: '{op}'.", stderr);
            return ExitCodes.UsageError;
        }

        if (!ExamplePaths.IsValidName(exampleName))
        {
            ConsoleColorWriter.WriteError($"Invalid --name segment: '{exampleName}'. Use a slug like 'default' or 'happy' ([a-z0-9] with optional hyphens).", stderr);
            return ExitCodes.UsageError;
        }

        if (requestSource is null && responseSource is null)
        {
            ConsoleColorWriter.WriteError("Provide at least one of --request or --response.", stderr);
            return ExitCodes.UsageError;
        }

        if (string.Equals(requestSource, "-", StringComparison.Ordinal)
            && string.Equals(responseSource, "-", StringComparison.Ordinal))
        {
            ConsoleColorWriter.WriteError("Only one of --request / --response may be '-" + "' (stdin).", stderr);
            return ExitCodes.UsageError;
        }

        var known = ExampleReferenceLinker.IndexOperationsByTagFile(skill);
        if (!known.ContainsKey(op))
        {
            ConsoleColorWriter.WriteError($"Unknown operationId '{op}' — not found in reference/*.md.", stderr);
            return ExitCodes.UsageError;
        }

        string? requestJson;
        string? responseJson;
        try
        {
            requestJson = ReadJsonSource(requestSource, stderr);
            responseJson = ReadJsonSource(responseSource, stderr);
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }

        if (requestJson is null && responseJson is null)
        {
            ConsoleColorWriter.WriteError("Provide at least one of --request or --response.", stderr);
            return ExitCodes.UsageError;
        }

        try
        {
            var (reqPath, resPath) = ExampleStore.Write(skill, op, exampleName, requestJson, responseJson, force);
            var orphans = ExampleReferenceLinker.SyncSkill(skill, ExampleReferenceLinker.KnownOperationIds(skill));
            WarnOrphans(orphans, stderr);

            stdout.WriteLine($"Added example {op}/{exampleName}");
            if (reqPath is not null)
            {
                stdout.WriteLine($"  request:  {Path.GetRelativePath(skill, reqPath)}");
            }

            if (resPath is not null)
            {
                stdout.WriteLine($"  response: {Path.GetRelativePath(skill, resPath)}");
            }

            return ExitCodes.Success;
        }
        catch (InvalidOperationException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
    }

    /// <summary>Testable <c>example list</c>.</summary>
    public static int RunList(string? skillDir, string? operationId, TextWriter stdout, TextWriter stderr)
    {
        if (!TryResolveSkillDir(skillDir, stderr, out var skill))
        {
            return ExitCodes.AcquisitionFailure;
        }

        var known = ExampleReferenceLinker.KnownOperationIds(skill);
        var discovery = ExampleStore.Discover(skill, known);
        var items = discovery.Items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            items = items.Where(i => string.Equals(i.OperationId, operationId.Trim(), StringComparison.Ordinal));
        }

        stdout.WriteLine("operationId\tname\thasRequest\thasResponse");
        foreach (var item in items)
        {
            stdout.WriteLine($"{item.OperationId}\t{item.Name}\t{item.HasRequest}\t{item.HasResponse}");
        }

        WarnOrphans(discovery.Orphans, stderr);
        return ExitCodes.Success;
    }

    /// <summary>Testable <c>example remove</c>.</summary>
    public static int RunRemove(
        string? skillDir,
        string? operationId,
        string? name,
        TextWriter stdout,
        TextWriter stderr)
    {
        if (!TryResolveSkillDir(skillDir, stderr, out var skill))
        {
            return ExitCodes.AcquisitionFailure;
        }

        if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(name))
        {
            ConsoleColorWriter.WriteError("--op and --name are required.", stderr);
            return ExitCodes.UsageError;
        }

        var op = operationId.Trim();
        var exampleName = ExamplePaths.NormalizeName(name);

        if (!ExampleStore.Remove(skill, op, exampleName))
        {
            ConsoleColorWriter.WriteError($"Example not found: {op}/{exampleName}", stderr);
            return ExitCodes.UsageError;
        }

        var orphans = ExampleReferenceLinker.SyncSkill(skill, ExampleReferenceLinker.KnownOperationIds(skill));
        WarnOrphans(orphans, stderr);
        stdout.WriteLine($"Removed example {op}/{exampleName}");
        return ExitCodes.Success;
    }

    /// <summary>Testable <c>example sync</c>.</summary>
    public static int RunSync(string? skillDir, TextWriter stdout, TextWriter stderr)
    {
        if (!TryResolveSkillDir(skillDir, stderr, out var skill))
        {
            return ExitCodes.AcquisitionFailure;
        }

        var known = ExampleReferenceLinker.KnownOperationIds(skill);
        var orphans = ExampleReferenceLinker.SyncSkill(skill, known);
        var count = ExampleStore.Discover(skill).Items.Count;
        stdout.WriteLine($"Synced {count} authored example(s) into reference/.");
        WarnOrphans(orphans, stderr);
        return ExitCodes.Success;
    }

    private static bool TryResolveSkillDir(string? skillDir, TextWriter stderr, out string skill)
    {
        skill = string.Empty;
        if (string.IsNullOrWhiteSpace(skillDir))
        {
            ConsoleColorWriter.WriteError("--skill is required.", stderr);
            return false;
        }

        skill = Path.GetFullPath(skillDir);
        if (!Directory.Exists(skill))
        {
            ConsoleColorWriter.WriteError($"Skill directory not found: {skill}", stderr);
            return false;
        }

        return true;
    }

    private static string? ReadJsonSource(string? source, TextWriter stderr)
    {
        if (source is null)
        {
            return null;
        }

        string text;
        if (source == "-")
        {
            text = Console.In.ReadToEnd();
        }
        else
        {
            var path = Path.GetFullPath(source);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"File not found: {path}");
            }

            text = File.ReadAllText(path);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Example JSON is empty.");
        }

        try
        {
            using var _ = JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            stderr.WriteLine($"Warning: content is not valid JSON ({ex.Message}); storing anyway.");
        }

        return text;
    }

    internal static void WarnOrphans(IReadOnlyList<string> orphans, TextWriter stderr)
    {
        if (orphans.Count == 0)
        {
            return;
        }

        stderr.WriteLine(
            $"Warning: orphan example operationId(s) not in reference/*.md (preserved): {string.Join(", ", orphans)}");
    }
}
