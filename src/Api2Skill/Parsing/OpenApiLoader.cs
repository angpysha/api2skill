using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace Api2Skill.Parsing;

/// <summary>One parse error/warning, mapped from Microsoft.OpenApi's OpenApiError (FR-010).</summary>
public sealed record ParseError(string Message, string? Pointer);

/// <summary>
/// Thrown when the input document is invalid or unparseable (FR-010, EC-1/EC-2). Callers
/// MUST treat this as "exit non-zero, emit no partial output" — never partially write a skill.
/// </summary>
public sealed class OpenApiParseException : Exception
{
    public OpenApiParseException(IReadOnlyList<ParseError> errors)
        : base(errors.Count > 0 ? errors[0].Message : "Failed to parse the OpenAPI document.")
    {
        Errors = errors;
    }

    public IReadOnlyList<ParseError> Errors { get; }
}

/// <summary>The successfully parsed document plus the diagnostics api2skill cares about.</summary>
public sealed record LoadedSpec(
    OpenApiDocument Document,
    OpenApiSpecVersion SpecVersion,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Thin wrapper over <c>OpenApiDocument.LoadAsync</c> (research.md R2). Always reads from an
/// already-buffered stream — acquisition (file/URL/stdin, TLS policy) is SpecSource's job, not
/// this type's (research.md R3): this class only turns bytes into a document or a clear error.
/// </summary>
public static class OpenApiLoader
{
    public static async Task<LoadedSpec> LoadAsync(
        Stream stream,
        string format,
        CancellationToken cancellationToken = default)
    {
        // YAML support lives in a separate package since OpenAPI.NET v2 and must be
        // registered explicitly — LoadAsync(..., "yaml", ...) throws "Format 'yaml' is not
        // supported" without this (confirmed empirically; not called out in the package docs).
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();

        ReadResult result;
        try
        {
            result = await OpenApiDocument.LoadAsync(stream, format, settings, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OpenApiParseException([new ParseError(ex.Message, null)]);
        }

        var diagnostic = result.Diagnostic;
        if (diagnostic?.Errors is { Count: > 0 } errors)
        {
            throw new OpenApiParseException(
                [.. errors.Select(e => new ParseError(e.Message, e.Pointer))]);
        }

        if (result.Document is null)
        {
            throw new OpenApiParseException([new ParseError("The document parsed to an empty result.", null)]);
        }

        var warnings = diagnostic?.Warnings is { Count: > 0 } w
            ? w.Select(e => e.Message).ToList()
            : [];

        return new LoadedSpec(
            result.Document,
            diagnostic?.SpecificationVersion ?? OpenApiSpecVersion.OpenApi3_0,
            warnings);
    }
}
