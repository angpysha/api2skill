namespace Api2Skill.Input;

/// <summary>
/// Acquires the OpenAPI/Swagger document into a buffered <see cref="MemoryStream"/> before
/// parsing (research.md R3). Buffering — rather than handing a live stream straight to the
/// reader — is required for stdin (non-seekable; Microsoft.OpenApi issue #2638) and is what
/// lets api2skill own the HTTP fetch for the URL source, so the untrusted-HTTPS opt-in
/// (FR-007) can apply to spec fetching too.
///
/// Foundational only wires the <b>file</b> path (WI-FOUND acceptance: parse-&gt;model-&gt;write
/// skeleton runs on the petstore fixture). URL fetch and stdin are added in US3 (T029) — this
/// class is designed to grow an <c>AcquireUrlAsync</c>/<c>AcquireStdinAsync</c> pair alongside
/// this method without changing its shape.
/// </summary>
public static class SpecSource
{
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
}
