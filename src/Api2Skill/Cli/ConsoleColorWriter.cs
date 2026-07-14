namespace Api2Skill.Cli;

/// <summary>
/// Colored stderr/stdout helpers for OAuth prompts and errors.
/// Disabled when <c>NO_COLOR</c> is set or the target stream is redirected (contracts/cli.md).
/// </summary>
public static class ConsoleColorWriter
{
    public static bool ColorsEnabled(TextWriter? writer = null)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
        {
            return false;
        }

        // When writing to Console.Error / Console.Out, respect redirection of that stream.
        if (writer is null || ReferenceEquals(writer, Console.Error))
        {
            return !Console.IsErrorRedirected;
        }

        if (ReferenceEquals(writer, Console.Out))
        {
            return !Console.IsOutputRedirected;
        }

        // Custom writers (tests): color is meaningless — skip ANSI-like state changes
        return false;
    }

    public static void WriteWarning(string message, TextWriter? writer = null)
    {
        WriteColored(message, ConsoleColor.Yellow, writer ?? Console.Error);
    }

    public static void WriteError(string message, TextWriter? writer = null)
    {
        WriteColored(message, ConsoleColor.Red, writer ?? Console.Error);
    }

    public static void WriteSuccess(string message, TextWriter? writer = null)
    {
        WriteColored(message, ConsoleColor.Green, writer ?? Console.Error);
    }

    public static void WriteInfo(string message, TextWriter? writer = null)
    {
        writer ??= Console.Error;
        writer.WriteLine(message);
    }

    private static void WriteColored(string message, ConsoleColor color, TextWriter writer)
    {
        if (!ColorsEnabled(writer))
        {
            writer.WriteLine(message);
            return;
        }

        var previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            writer.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = previous;
        }
    }
}
