using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

/// <summary>
/// In-process hosted OAuth relay matching <c>contracts/hosted-relay.md</c> (no cloud required).
/// </summary>
public sealed class TestHostedRelayServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private int _disposed;

    public Uri BaseUri { get; }

    private TestHostedRelayServer(HttpListener listener, Uri baseUri)
    {
        _listener = listener;
        BaseUri = baseUri;
    }

    public static async Task<TestHostedRelayServer> StartAsync()
    {
        // Reuse bind-with-retry: GetFreePort + Start has a TOCTOU race under xUnit parallelism.
        var (listener, port) = LoopbackHttpListenerFactory.Start();
        var server = new TestHostedRelayServer(listener, new Uri($"http://127.0.0.1:{port}/"));
        server._loop = Task.Run(server.AcceptLoopAsync);
        // Brief settle so first request never races Start
        await Task.Delay(10).ConfigureAwait(false);
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // already closed
        }
        catch (HttpListenerException)
        {
            // Managed HttpListener can throw "Address already in use" from Close() when other
            // listeners share the process registry — see LoopbackHttpCollection.
        }

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // listener closed mid-accept
            }
        }

        _cts.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && path.Equals("/v1/session", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSessionAsync(context).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && path.Equals("/v1/callback", StringComparison.OrdinalIgnoreCase))
            {
                HandleCallback(context);
                return;
            }

            if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && path.Equals("/v1/poll", StringComparison.OrdinalIgnoreCase))
            {
                HandlePoll(context);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task HandleSessionAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        var req = string.IsNullOrWhiteSpace(body)
            ? new HostedSessionCreateRequest(null, null)
            : JsonSerializer.Deserialize(body, HostedRelayJsonContext.Default.HostedSessionCreateRequest)
              ?? new HostedSessionCreateRequest(null, null);

        var ttl = Math.Clamp(req.TtlSeconds ?? 300, 1, 300);
        var sessionId = Guid.NewGuid().ToString("N");
        var expires = DateTimeOffset.UtcNow.AddSeconds(ttl);
        var callbackUrl = new Uri(BaseUri, $"v1/callback?sid={Uri.EscapeDataString(sessionId)}").ToString();

        _sessions[sessionId] = new Session(
            SessionId: sessionId,
            State: req.State,
            ExpiresUtc: expires,
            Code: null,
            Error: null,
            ErrorDescription: null,
            Consumed: false,
            Completed: false);

        var response = new HostedSessionCreateResponse(sessionId, callbackUrl, expires);
        var json = JsonSerializer.Serialize(response, HostedRelayJsonContext.Default.HostedSessionCreateResponse);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = 201;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    private void HandleCallback(HttpListenerContext context)
    {
        var query = ParseQuery(context.Request.Url?.Query ?? string.Empty);
        query.TryGetValue("sid", out var sid);
        if (string.IsNullOrEmpty(sid) || !_sessions.TryGetValue(sid, out var session))
        {
            WriteHtml(context, 404, "Unknown or expired session.");
            return;
        }

        if (DateTimeOffset.UtcNow > session.ExpiresUtc || session.Consumed)
        {
            _sessions.TryRemove(sid, out _);
            WriteHtml(context, 410, "Session expired.");
            return;
        }

        query.TryGetValue("code", out var code);
        query.TryGetValue("state", out var state);
        query.TryGetValue("error", out var error);
        query.TryGetValue("error_description", out var errorDescription);

        var updated = session with
        {
            Code = string.IsNullOrEmpty(code) ? null : code,
            State = string.IsNullOrEmpty(state) ? session.State : state,
            Error = string.IsNullOrEmpty(error) ? null : error,
            ErrorDescription = string.IsNullOrEmpty(errorDescription) ? null : errorDescription,
            Completed = true,
        };
        _sessions[sid] = updated;

        WriteHtml(context, 200, "You can close this window and return to the terminal.");
    }

    private void HandlePoll(HttpListenerContext context)
    {
        var query = ParseQuery(context.Request.Url?.Query ?? string.Empty);
        query.TryGetValue("sid", out var sid);
        query.TryGetValue("state", out var stateKey);

        Session? session = null;
        string? key = null;
        if (!string.IsNullOrEmpty(sid) && _sessions.TryGetValue(sid, out session))
        {
            key = sid;
        }
        else if (!string.IsNullOrEmpty(stateKey))
        {
            foreach (var pair in _sessions)
            {
                if (string.Equals(pair.Value.State, stateKey, StringComparison.Ordinal))
                {
                    session = pair.Value;
                    key = pair.Key;
                    break;
                }
            }
        }

        if (session is null || key is null)
        {
            context.Response.StatusCode = 410;
            context.Response.Close();
            return;
        }

        if (DateTimeOffset.UtcNow > session.ExpiresUtc)
        {
            _sessions.TryRemove(key, out _);
            context.Response.StatusCode = 410;
            context.Response.Close();
            return;
        }

        if (session.Consumed)
        {
            context.Response.StatusCode = 410;
            context.Response.Close();
            return;
        }

        if (!session.Completed)
        {
            WriteJson(context, 200, new HostedPollResponse("pending", null, null, null, null));
            return;
        }

        _sessions[key] = session with { Consumed = true };
        WriteJson(
            context,
            200,
            new HostedPollResponse(
                "completed",
                session.Code,
                session.State,
                session.Error,
                session.ErrorDescription));
    }

    private static void WriteHtml(HttpListenerContext context, int status, string message)
    {
        var html = $"<html><body>{WebUtility.HtmlEncode(message)}</body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();
    }

    private static void WriteJson(HttpListenerContext context, int status, HostedPollResponse body)
    {
        var json = JsonSerializer.Serialize(body, HostedRelayJsonContext.Default.HostedPollResponse);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();
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

    private sealed record Session(
        string SessionId,
        string? State,
        DateTimeOffset ExpiresUtc,
        string? Code,
        string? Error,
        string? ErrorDescription,
        bool Consumed,
        bool Completed);
}
