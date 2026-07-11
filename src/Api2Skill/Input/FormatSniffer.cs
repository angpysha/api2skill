namespace Api2Skill.Input;

/// <summary>
/// Resolves whether a buffered spec document is JSON or YAML, for
/// <c>OpenApiDocument.LoadAsync(stream, format, ...)</c> (research.md R2/R3). Precedence:
/// explicit override (--format) &gt; file extension &gt; content sniff. Content sniffing
/// looks at the first non-whitespace byte; needed for stdin/URL sources that have no
/// extension to go on.
/// </summary>
public static class FormatSniffer
{
    public const string Json = "json";
    public const string Yaml = "yaml";

    public static string? FromExtension(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => Json,
            ".yaml" or ".yml" => Yaml,
            _ => null,
        };
    }

    public static string FromContent(ReadOnlySpan<byte> content)
    {
        foreach (var b in content)
        {
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return b is (byte)'{' or (byte)'[' ? Json : Yaml;
        }

        // Empty/whitespace-only content: default to YAML (a superset syntactically) and let
        // the parser report the real "empty document" error rather than guessing further.
        return Yaml;
    }

    public static string Resolve(string? explicitFormat, string? path, ReadOnlySpan<byte> content)
    {
        if (!string.IsNullOrWhiteSpace(explicitFormat))
        {
            return explicitFormat.ToLowerInvariant();
        }

        return FromExtension(path) ?? FromContent(content);
    }
}
