using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace Api2Skill.OAuth;

/// <summary>
/// Custom URL-scheme redirect capture via OS second-instance handoff.
/// The waiting <c>oauth-capture</c> hosts a named pipe; the OS-launched process delivers the
/// callback URI with <see cref="DeliverHandoffAsync"/>.
/// </summary>
public sealed class CustomSchemeCapture : IRedirectCapture
{
    private static readonly ConcurrentDictionary<string, byte> WaitingPipes = new(StringComparer.Ordinal);

    public static string PipeNameForScheme(string scheme) =>
        "api2skill-oauth-" + scheme.ToLowerInvariant();

    /// <summary>Test hook: true while a capture is listening for <paramref name="pipeName"/>.</summary>
    public static bool IsPipeWaiting(string pipeName) => WaitingPipes.ContainsKey(pipeName);

    public async Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        var uri = options.CallbackUrl;
        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CustomSchemeCapture requires a non-http(s) callback URL, got {uri.Scheme}.");
        }

        var pipeName = PipeNameForScheme(uri.Scheme);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.EffectiveTimeout);

        WaitingPipes[pipeName] = 0;
        try
        {
            await using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return TimeoutResult(options);
            }

            string? handedOff;
            using (var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                try
                {
                    handedOff = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return TimeoutResult(options);
                }
            }

            if (string.IsNullOrWhiteSpace(handedOff)
                || !Uri.TryCreate(handedOff.Trim(), UriKind.Absolute, out var callbackUri))
            {
                return new CaptureResult(
                    Ok: false,
                    Mode: nameof(CaptureMode.CustomScheme),
                    Code: null,
                    State: options.State,
                    Error: "invalid_handoff",
                    ErrorDescription: "Received an empty or invalid callback URI from the OS handler.",
                    CallbackUrl: uri.ToString());
            }

            return BuildResult(options, callbackUri);
        }
        finally
        {
            WaitingPipes.TryRemove(pipeName, out _);
        }
    }

    /// <summary>
    /// Second-instance entry: deliver <paramref name="callbackUriString"/> to a waiting capture.
    /// Returns process exit code (0 = delivered).
    /// </summary>
    public static async Task<int> DeliverHandoffAsync(string callbackUriString, TextWriter? stderr = null)
    {
        stderr ??= Console.Error;

        if (!Uri.TryCreate(callbackUriString, UriKind.Absolute, out var uri)
            || uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            stderr.WriteLine("Not a custom-scheme OAuth callback URI.");
            return 2;
        }

        var pipeName = PipeNameForScheme(uri.Scheme);
        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(connectCts.Token).ConfigureAwait(false);

            await using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(uri.AbsoluteUri).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or OperationCanceledException)
        {
            stderr.WriteLine(
                "No oauth-capture session is waiting for this callback. " +
                "Start `api2skill oauth-capture --callback-url '…'` before completing browser login.");
            return 6;
        }
    }

    private static CaptureResult TimeoutResult(CaptureOptions options)
    {
        var seconds = (int)Math.Max(1, options.EffectiveTimeout.TotalSeconds);
        return new CaptureResult(
            Ok: false,
            Mode: nameof(CaptureMode.CustomScheme),
            Code: null,
            State: options.State,
            Error: "timeout",
            ErrorDescription: $"No redirect received within {seconds} seconds",
            CallbackUrl: options.CallbackUrl.ToString());
    }

    private static CaptureResult BuildResult(CaptureOptions options, Uri handedOff)
    {
        var query = ParseQuery(handedOff.Query);
        query.TryGetValue("code", out var code);
        query.TryGetValue("state", out var state);
        query.TryGetValue("error", out var error);
        query.TryGetValue("error_description", out var errorDescription);

        if (!string.IsNullOrEmpty(options.State)
            && !string.IsNullOrEmpty(state)
            && !string.Equals(options.State, state, StringComparison.Ordinal))
        {
            return new CaptureResult(
                Ok: false,
                Mode: nameof(CaptureMode.CustomScheme),
                Code: null,
                State: state,
                Error: "state_mismatch",
                ErrorDescription: "Returned state did not match --state.",
                CallbackUrl: options.CallbackUrl.ToString());
        }

        var ok = !string.IsNullOrEmpty(code) && string.IsNullOrEmpty(error);
        return new CaptureResult(
            Ok: ok,
            Mode: nameof(CaptureMode.CustomScheme),
            Code: string.IsNullOrEmpty(code) ? null : code,
            State: state,
            Error: string.IsNullOrEmpty(error) ? null : error,
            ErrorDescription: string.IsNullOrEmpty(errorDescription) ? null : errorDescription,
            CallbackUrl: options.CallbackUrl.ToString());
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        var q = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..eq]);
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
