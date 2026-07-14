using System.Net;

namespace Api2Skill.OAuth;

/// <summary>
/// HTTP loopback redirect capture via <see cref="HttpListener"/> (app-owned).
/// Dual <c>localhost</c>/<c>127.0.0.1</c> prefixes; ignores favicon; listens before awaiting query.
/// </summary>
public sealed class LoopbackHttpCapture : IRedirectCapture
{
    private static readonly byte[] SuccessHtml = System.Text.Encoding.UTF8.GetBytes(
        "<html><body>Login complete — you can close this window and return to the terminal.</body></html>");

    public async Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        var uri = options.CallbackUrl;
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"LoopbackHttpCapture requires an http:// callback URL, got {uri.Scheme}.");
        }

        using var listener = new HttpListener();
        AddCallbackPrefixes(listener, uri);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                $"Could not start the local OAuth callback listener on {uri.Host}:{uri.Port} ({ex.Message}).",
                ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.EffectiveTimeout);

        try
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().WaitAsync(timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var path = context.Request.Url?.AbsolutePath ?? "/";
                if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var query = ParseQuery(context.Request.Url?.Query ?? string.Empty);
                if (query.TryGetValue("code", out _) || query.TryGetValue("error", out _))
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = SuccessHtml.Length;
                    await context.Response.OutputStream.WriteAsync(SuccessHtml, CancellationToken.None)
                        .ConfigureAwait(false);
                    context.Response.Close();

                    return BuildResult(options, query);
                }

                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
        finally
        {
            try
            {
                if (listener.IsListening)
                {
                    listener.Stop();
                }

                listener.Close();
            }
            catch (ObjectDisposedException)
            {
                // already closed
            }
        }

        var seconds = (int)options.EffectiveTimeout.TotalSeconds;
        return new CaptureResult(
            Ok: false,
            Mode: nameof(CaptureMode.HttpLoopback),
            Code: null,
            State: options.State,
            Error: "timeout",
            ErrorDescription: $"No redirect received within {seconds} seconds",
            CallbackUrl: uri.ToString());
    }

    internal static void AddCallbackPrefixes(HttpListener listener, Uri uri)
    {
        var scheme = uri.Scheme;
        var port = uri.IsDefaultPort
            ? (scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : uri.Port;
        var host = uri.Host;

        listener.Prefixes.Add($"{scheme}://{host}:{port}/");
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            listener.Prefixes.Add($"{scheme}://127.0.0.1:{port}/");
        }
        else if (host is "127.0.0.1" or "::1" or "[::1]")
        {
            listener.Prefixes.Add($"{scheme}://localhost:{port}/");
        }
    }

    private static CaptureResult BuildResult(CaptureOptions options, Dictionary<string, string> query)
    {
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
                Mode: nameof(CaptureMode.HttpLoopback),
                Code: null,
                State: state,
                Error: "state_mismatch",
                ErrorDescription: "Returned state did not match --state.",
                CallbackUrl: options.CallbackUrl.ToString());
        }

        var ok = !string.IsNullOrEmpty(code) && string.IsNullOrEmpty(error);
        return new CaptureResult(
            Ok: ok,
            Mode: nameof(CaptureMode.HttpLoopback),
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
