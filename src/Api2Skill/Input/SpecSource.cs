namespace Api2Skill.Input;

/// <summary>
/// Acquires the OpenAPI/Swagger document into a buffered <see cref="MemoryStream"/> before
/// parsing (research.md R3). Buffering — rather than handing a live stream straight to the
/// reader — is required for stdin (non-seekable; Microsoft.OpenApi issue #2638) and is what
/// lets api2skill own the HTTP fetch for the URL source, so the untrusted-HTTPS opt-in
/// (FR-007) can apply to spec fetching too.
/// </summary>
public static class SpecSource
{
    /// <summary>
    /// Routes to file / URL / stdin acquisition based on the shape of <paramref name="source"/>
    /// (contracts/cli.md): <c>-</c> means stdin, <c>http://</c>/<c>https://</c> means URL,
    /// anything else is a local file path.
    /// </summary>
    public static Task<(MemoryStream Stream, string Format)> AcquireAsync(
        string source, string? formatOverride, bool insecure, CancellationToken cancellationToken = default)
    {
        if (source == "-")
        {
            return AcquireStdinAsync(formatOverride, cancellationToken);
        }

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return AcquireUrlAsync(source, insecure, formatOverride, cancellationToken);
        }

        return AcquireFileAsync(source, formatOverride, cancellationToken);
    }

    public static async Task<(MemoryStream Stream, string Format)> AcquireFileAsync(
        string path, string? formatOverride, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Spec file not found: {path}", path);
        }

        var buffer = new MemoryStream();
        await using (var file = File.OpenRead(path))
        {
            await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        var format = FormatSniffer.Resolve(formatOverride, path, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        buffer.Position = 0;
        return (buffer, format);
    }

    /// <summary>
    /// Fetches the spec via HTTP(S). Owns the <see cref="HttpClient"/> itself (rather than
    /// delegating to Microsoft.OpenApi's own URL loader) specifically so the untrusted-HTTPS
    /// opt-in (<paramref name="insecure"/>, FR-007/EC-8) can apply here too, for dev hosts with
    /// self-signed certificates.
    /// </summary>
    public static async Task<(MemoryStream Stream, string Format)> AcquireUrlAsync(
        string url, bool insecure, string? formatOverride, CancellationToken cancellationToken = default)
    {
        using var handler = new HttpClientHandler();
        if (insecure)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        using var http = new HttpClient(handler);

        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var buffer = new MemoryStream();
        await using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            await responseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        var format = FormatSniffer.Resolve(formatOverride, url, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        buffer.Position = 0;
        return (buffer, format);
    }

    /// <summary>
    /// Reads stdin to completion into a buffer. Required rather than passing the live stream
    /// straight through: stdin is non-seekable, and Microsoft.OpenApi's <c>LoadAsync</c> fails
    /// to read a non-seekable JSON stream without an explicit format
    /// (https://github.com/Microsoft/OpenAPI.NET/issues/2638) — buffering sidesteps that for
    /// every source uniformly.
    /// </summary>
    public static async Task<(MemoryStream Stream, string Format)> AcquireStdinAsync(
        string? formatOverride, CancellationToken cancellationToken = default)
    {
        var buffer = new MemoryStream();
        await using (var stdin = Console.OpenStandardInput())
        {
            await stdin.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        var format = FormatSniffer.Resolve(formatOverride, path: null, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        buffer.Position = 0;
        return (buffer, format);
    }
}
