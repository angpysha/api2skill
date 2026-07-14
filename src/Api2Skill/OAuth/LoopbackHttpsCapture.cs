using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Api2Skill.OAuth;

/// <summary>
/// HTTPS loopback redirect capture via Kestrel (app-owned; never emitted into skills).
/// Dual localhost/127.0.0.1 bind via <c>ListenLocalhost</c>; ignores favicon; listens before awaiting query.
/// </summary>
public sealed class LoopbackHttpsCapture : IRedirectCapture
{
    private static readonly byte[] SuccessHtml = System.Text.Encoding.UTF8.GetBytes(
        "<html><body>Login complete — you can close this window and return to the terminal.</body></html>");

    private readonly X509Certificate2 _certificate;

    public LoopbackHttpsCapture(X509Certificate2 certificate)
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
    }

    public async Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default)
    {
        var uri = options.CallbackUrl;
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"LoopbackHttpsCapture requires an https:// callback URL, got {uri.Scheme}.");
        }

        var port = uri.IsDefaultPort ? 443 : uri.Port;
        var resultTcs = new TaskCompletionSource<CaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls();
        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ListenLocalhost(port, listenOptions =>
            {
                listenOptions.UseHttps(_certificate);
            });
        });

        await using var app = builder.Build();

        app.Run(async context =>
        {
            var path = context.Request.Path.Value ?? "/";
            if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var query = context.Request.Query;
            if (!query.ContainsKey("code") && !query.ContainsKey("error"))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query)
            {
                dict[pair.Key] = pair.Value.ToString();
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.Body.WriteAsync(SuccessHtml, CancellationToken.None).ConfigureAwait(false);
            resultTcs.TrySetResult(BuildResult(options, dict));
        });

        try
        {
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start the local HTTPS OAuth callback listener on {uri.Host}:{port} ({ex.Message}).",
                ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.EffectiveTimeout);

        try
        {
            await using (timeoutCts.Token.Register(() => resultTcs.TrySetCanceled(timeoutCts.Token)))
            {
                try
                {
                    return await resultTcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    var seconds = (int)Math.Max(1, options.EffectiveTimeout.TotalSeconds);
                    return new CaptureResult(
                        Ok: false,
                        Mode: nameof(CaptureMode.HttpsLoopback),
                        Code: null,
                        State: options.State,
                        Error: "timeout",
                        ErrorDescription: $"No redirect received within {seconds} seconds",
                        CallbackUrl: uri.ToString());
                }
            }
        }
        finally
        {
            try { await app.StopAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception) { /* best-effort */ }
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
                Mode: nameof(CaptureMode.HttpsLoopback),
                Code: null,
                State: state,
                Error: "state_mismatch",
                ErrorDescription: "Returned state did not match --state.",
                CallbackUrl: options.CallbackUrl.ToString());
        }

        var ok = !string.IsNullOrEmpty(code) && string.IsNullOrEmpty(error);
        return new CaptureResult(
            Ok: ok,
            Mode: nameof(CaptureMode.HttpsLoopback),
            Code: string.IsNullOrEmpty(code) ? null : code,
            State: state,
            Error: string.IsNullOrEmpty(error) ? null : error,
            ErrorDescription: string.IsNullOrEmpty(errorDescription) ? null : errorDescription,
            CallbackUrl: options.CallbackUrl.ToString());
    }
}
