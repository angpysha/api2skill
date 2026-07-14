namespace Api2Skill.Examples;

/// <summary>One named example folder under <c>examples/&lt;op&gt;/&lt;name&gt;/</c>.</summary>
public sealed record ExampleArtifact(
    string OperationId,
    string Name,
    bool HasRequest,
    bool HasResponse);

/// <summary>Filesystem scan of a skill's <c>examples/</c> tree.</summary>
public sealed record ExampleDiscoveryResult(
    IReadOnlyList<ExampleArtifact> Items,
    IReadOnlyList<string> Orphans);

/// <summary>
/// Discover, write, and remove authored example files (data-model.md).
/// Does not mutate <c>reference/*.md</c> — see <see cref="ExampleReferenceLinker"/>.
/// </summary>
public static class ExampleStore
{
    public static ExampleDiscoveryResult Discover(string skillDirectory, IReadOnlySet<string>? knownOperationIds = null)
    {
        var root = ExamplePaths.ExamplesRoot(skillDirectory);
        if (!Directory.Exists(root))
        {
            return new ExampleDiscoveryResult([], []);
        }

        var items = new List<ExampleArtifact>();
        var orphanOps = new HashSet<string>(StringComparer.Ordinal);

        foreach (var opDir in Directory.EnumerateDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
        {
            var operationId = Path.GetFileName(opDir);
            if (!ExamplePaths.IsSafePathSegment(operationId))
            {
                continue;
            }

            var isOrphan = knownOperationIds is not null && !knownOperationIds.Contains(operationId);
            if (isOrphan)
            {
                orphanOps.Add(operationId);
            }

            foreach (var nameDir in Directory.EnumerateDirectories(opDir).OrderBy(d => d, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(nameDir);
                if (!ExamplePaths.IsSafePathSegment(name))
                {
                    continue;
                }

                var hasRequest = File.Exists(Path.Combine(nameDir, ExamplePaths.RequestFileName));
                var hasResponse = File.Exists(Path.Combine(nameDir, ExamplePaths.ResponseFileName));
                if (!hasRequest && !hasResponse)
                {
                    continue;
                }

                items.Add(new ExampleArtifact(operationId, name, hasRequest, hasResponse));
            }
        }

        return new ExampleDiscoveryResult(
            items,
            orphanOps.OrderBy(o => o, StringComparer.Ordinal).ToList());
    }

    public static IReadOnlyList<ExampleArtifact> ForOperation(string skillDirectory, string operationId)
    {
        var discovery = Discover(skillDirectory);
        return discovery.Items
            .Where(i => string.Equals(i.OperationId, operationId, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Write request and/or response JSON for a named example. Returns absolute paths written.</summary>
    public static (string? RequestPath, string? ResponsePath) Write(
        string skillDirectory,
        string operationId,
        string name,
        string? requestJson,
        string? responseJson,
        bool force)
    {
        if (requestJson is null && responseJson is null)
        {
            throw new ArgumentException("At least one of request or response JSON is required.");
        }

        var dir = ExamplePaths.ExampleDirectory(skillDirectory, operationId, name);
        var requestPath = ExamplePaths.RequestPath(skillDirectory, operationId, name);
        var responsePath = ExamplePaths.ResponsePath(skillDirectory, operationId, name);

        if (!force)
        {
            if (requestJson is not null && File.Exists(requestPath))
            {
                throw new InvalidOperationException($"Example request already exists: {requestPath}. Pass --force to overwrite.");
            }

            if (responseJson is not null && File.Exists(responsePath))
            {
                throw new InvalidOperationException($"Example response already exists: {responsePath}. Pass --force to overwrite.");
            }
        }

        Directory.CreateDirectory(dir);

        string? writtenRequest = null;
        string? writtenResponse = null;

        if (requestJson is not null)
        {
            File.WriteAllText(requestPath, NormalizeJsonTrailingNewline(requestJson));
            writtenRequest = requestPath;
        }

        if (responseJson is not null)
        {
            File.WriteAllText(responsePath, NormalizeJsonTrailingNewline(responseJson));
            writtenResponse = responsePath;
        }

        return (writtenRequest, writtenResponse);
    }

    public static bool Remove(string skillDirectory, string operationId, string name)
    {
        var dir = ExamplePaths.ExampleDirectory(skillDirectory, operationId, name);
        if (!Directory.Exists(dir))
        {
            return false;
        }

        Directory.Delete(dir, recursive: true);

        var opDir = Path.Combine(ExamplePaths.ExamplesRoot(skillDirectory), operationId);
        if (Directory.Exists(opDir) && !Directory.EnumerateFileSystemEntries(opDir).Any())
        {
            Directory.Delete(opDir);
        }

        var root = ExamplePaths.ExamplesRoot(skillDirectory);
        if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
        {
            Directory.Delete(root);
        }

        return true;
    }

    /// <summary>
    /// Copy an existing <c>examples/</c> tree into <paramref name="destinationSkillDirectory"/>
    /// (SkillWriter staging preservation).
    /// </summary>
    public static void CopyTree(string sourceSkillDirectory, string destinationSkillDirectory)
    {
        var source = ExamplePaths.ExamplesRoot(sourceSkillDirectory);
        if (!Directory.Exists(source))
        {
            return;
        }

        var dest = ExamplePaths.ExamplesRoot(destinationSkillDirectory);
        CopyDirectoryRecursive(source, dest);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var target = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            CopyDirectoryRecursive(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    private static string NormalizeJsonTrailingNewline(string json)
    {
        var trimmed = json.TrimEnd('\r', '\n');
        return trimmed + Environment.NewLine;
    }
}
