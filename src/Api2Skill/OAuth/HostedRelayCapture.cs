using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api2Skill.OAuth;

/// <summary>
/// Hosted OAuth relay client: create session, print callback URL, poll until code/error/timeout.
/// Relay base from <see cref="CaptureOptions.RelayBaseUrl"/>, <c>API2SKILL_OAUTH_RELAY_BASE</c>,
/// or <see cref="CaptureModeResolver.DefaultRelayBase"/>.
/// </summary>
public sealed class HostedRelayCapture : IRedirectCapture
{
    private readonly HttpClient _http;
    private readonly TextWriter? _progress;
    private readonly TimeSpan _pollInterval;

    public HostedRelayCapture(
        HttpClient? httpClient = null,
        TextWriter? progress = null,
        TimeSpan? pollInterval = null)
    {
        _http = httpClient ?? new HttpClient();
        _progress = progress;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    public async Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        var relayBase = ResolveRelayBase(options);
        var ttlSeconds = Math.Clamp((int)Math.Ceiling(options.EffectiveTimeout.TotalSeconds), 1, 300);
        var createRequest = new HostedSessionCreateRequest(options.State, ttlSeconds);
        using var createContent = JsonContent.Create(
            createRequest,
            HostedRelayJsonContext.Default.HostedSessionCreateRequest);

        HostedSessionCreateResponse session;
        try
        {
            using var createResponse = await _http
                .PostAsync(Combine(relayBase, "/v1/session"), createContent, cancellationToken)
                .ConfigureAwait(false);
            if (createResponse.StatusCode != HttpStatusCode.Created
                && createResponse.StatusCode != HttpStatusCode.OK)
            {
                var detail = await createResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"Hosted relay session create failed ({(int)createResponse.StatusCode}): {Truncate(detail)}");
            }

            await using var stream = await createResponse.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            session = (await JsonSerializer
                .DeserializeAsync(stream, HostedRelayJsonContext.Default.HostedSessionCreateResponse, cancellationToken)
                .ConfigureAwait(false))
                ?? throw new InvalidOperationException("Hosted relay returned an empty session response.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach hosted OAuth relay at {relayBase} ({ex.Message}).",
                ex);
        }

        if (string.IsNullOrWhiteSpace(session.SessionId) || string.IsNullOrWhiteSpace(session.CallbackUrl))
        {
            throw new InvalidOperationException("Hosted relay session response missing sessionId or callbackUrl.");
        }

        _progress?.WriteLine($"Hosted callback URL: {session.CallbackUrl}");
        _progress?.Flush();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.EffectiveTimeout);

        try
        {
            while (!timeoutCts.IsCancellationRequested)
            {
                var pollUri = Combine(relayBase, $"/v1/poll?sid={Uri.EscapeDataString(session.SessionId)}");
                using var pollResponse = await _http.GetAsync(pollUri, timeoutCts.Token).ConfigureAwait(false);

                if (pollResponse.StatusCode == HttpStatusCode.Gone)
                {
                    return new CaptureResult(
                        Ok: false,
                        Mode: nameof(CaptureMode.Hosted),
                        Code: null,
                        State: options.State,
                        Error: "session_expired",
                        ErrorDescription: "Hosted relay session expired or was already consumed.",
                        CallbackUrl: session.CallbackUrl);
                }

                if (!pollResponse.IsSuccessStatusCode)
                {
                    var detail = await pollResponse.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"Hosted relay poll failed ({(int)pollResponse.StatusCode}): {Truncate(detail)}");
                }

                await using var pollStream = await pollResponse.Content.ReadAsStreamAsync(timeoutCts.Token)
                    .ConfigureAwait(false);
                var poll = await JsonSerializer
                    .DeserializeAsync(pollStream, HostedRelayJsonContext.Default.HostedPollResponse, timeoutCts.Token)
                    .ConfigureAwait(false);

                if (poll is null)
                {
                    throw new InvalidOperationException("Hosted relay returned an empty poll response.");
                }

                if (string.Equals(poll.Status, "pending", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Delay(_pollInterval, timeoutCts.Token).ConfigureAwait(false);
                    continue;
                }

                if (string.Equals(poll.Status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    return BuildCompleted(options, session.CallbackUrl, poll);
                }

                throw new InvalidOperationException($"Unexpected hosted relay poll status: {poll.Status}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // timeout
        }

        var seconds = (int)options.EffectiveTimeout.TotalSeconds;
        return new CaptureResult(
            Ok: false,
            Mode: nameof(CaptureMode.Hosted),
            Code: null,
            State: options.State,
            Error: "timeout",
            ErrorDescription: $"No redirect received within {seconds} seconds",
            CallbackUrl: session.CallbackUrl);
    }

    internal static string ResolveRelayBase(CaptureOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.RelayBaseUrl))
        {
            return options.RelayBaseUrl.TrimEnd('/');
        }

        var env = Environment.GetEnvironmentVariable("API2SKILL_OAUTH_RELAY_BASE");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.TrimEnd('/');
        }

        // Derive from callback URL when it already points at a relay callback path
        var cb = options.CallbackUrl;
        if (cb.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            || cb.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(cb.Scheme, cb.Host, cb.IsDefaultPort ? -1 : cb.Port);
            return builder.Uri.ToString().TrimEnd('/');
        }

        return CaptureModeResolver.DefaultRelayBase.TrimEnd('/');
    }

    private static CaptureResult BuildCompleted(
        CaptureOptions options,
        string callbackUrl,
        HostedPollResponse poll)
    {
        if (!string.IsNullOrEmpty(options.State)
            && !string.IsNullOrEmpty(poll.State)
            && !string.Equals(options.State, poll.State, StringComparison.Ordinal))
        {
            return new CaptureResult(
                Ok: false,
                Mode: nameof(CaptureMode.Hosted),
                Code: null,
                State: poll.State,
                Error: "state_mismatch",
                ErrorDescription: "Returned state did not match --state.",
                CallbackUrl: callbackUrl);
        }

        var ok = !string.IsNullOrEmpty(poll.Code) && string.IsNullOrEmpty(poll.Error);
        return new CaptureResult(
            Ok: ok,
            Mode: nameof(CaptureMode.Hosted),
            Code: string.IsNullOrEmpty(poll.Code) ? null : poll.Code,
            State: poll.State ?? options.State,
            Error: string.IsNullOrEmpty(poll.Error) ? null : poll.Error,
            ErrorDescription: string.IsNullOrEmpty(poll.ErrorDescription) ? null : poll.ErrorDescription,
            CallbackUrl: callbackUrl);
    }

    private static Uri Combine(string relayBase, string pathAndQuery)
    {
        var baseUri = relayBase.EndsWith('/') ? new Uri(relayBase) : new Uri(relayBase + "/");
        return new Uri(baseUri, pathAndQuery.TrimStart('/'));
    }

    private static string Truncate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Length <= 200 ? text : text[..200] + "…";
    }
}
