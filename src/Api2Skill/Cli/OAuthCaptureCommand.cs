using System.CommandLine;
using System.Text.Json;
using Api2Skill.OAuth;

namespace Api2Skill.Cli;

/// <summary>
/// <c>api2skill oauth-capture</c> — thin redirect capture (no token exchange).
/// Exit codes 2/6/7 per contracts/cli.md (009).
/// </summary>
public static class OAuthCaptureCommand
{
    public static Command Create()
    {
        var callbackUrlOption = new Option<string>("--callback-url")
        {
            Description = "Redirect URI registered at the IdP.",
            Required = true,
        };
        var modeOption = new Option<string>("--mode")
        {
            Description = "Capture mode: auto|http|https|scheme|hosted (default auto).",
            DefaultValueFactory = _ => "auto",
        };
        var timeoutOption = new Option<int>("--timeout")
        {
            Description = "Seconds to wait for the redirect (default 180).",
            DefaultValueFactory = _ => 180,
        };
        var certOption = new Option<string?>("--cert")
        {
            Description = "PFX/PKCS#12 path for HTTPS loopback.",
        };
        var certPasswordOption = new Option<string?>("--cert-password")
        {
            Description = "PFX password (prompted on TTY if needed).",
        };
        var certPemOption = new Option<string?>("--cert-pem")
        {
            Description = "PEM certificate path (with --cert-key) for HTTPS loopback.",
        };
        var certKeyOption = new Option<string?>("--cert-key")
        {
            Description = "PEM private key path (with --cert-pem) for HTTPS loopback.",
        };
        var relayBaseOption = new Option<string?>("--relay-base")
        {
            Description = "Hosted relay base URL (overrides API2SKILL_OAUTH_RELAY_BASE).",
        };
        var stateOption = new Option<string?>("--state")
        {
            Description = "Expected OAuth state; mismatch yields ok:false.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Force JSON stdout (default for scripting).",
            DefaultValueFactory = _ => true,
        };

        var command = new Command("oauth-capture", "Capture an OAuth redirect (code/error) without exchanging tokens")
        {
            callbackUrlOption,
            modeOption,
            timeoutOption,
            certOption,
            certPasswordOption,
            certPemOption,
            certKeyOption,
            relayBaseOption,
            stateOption,
            jsonOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var exit = await RunAsync(
                callbackUrl: parseResult.GetValue(callbackUrlOption),
                mode: parseResult.GetValue(modeOption),
                timeoutSeconds: parseResult.GetValue(timeoutOption),
                certPath: parseResult.GetValue(certOption),
                certPassword: parseResult.GetValue(certPasswordOption),
                certPemPath: parseResult.GetValue(certPemOption),
                certKeyPath: parseResult.GetValue(certKeyOption),
                relayBase: parseResult.GetValue(relayBaseOption),
                state: parseResult.GetValue(stateOption),
                json: parseResult.GetValue(jsonOption),
                isInteractive: !Console.IsInputRedirected,
                stdout: Console.Out,
                stderr: Console.Error,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return exit;
        });

        return command;
    }

    /// <summary>
    /// Testable entry point for <c>oauth-capture</c>.
    /// </summary>
    public static async Task<int> RunAsync(
        string? callbackUrl,
        string? mode = "auto",
        int timeoutSeconds = 180,
        string? certPath = null,
        string? certPassword = null,
        string? certPemPath = null,
        string? certKeyPath = null,
        string? relayBase = null,
        string? state = null,
        bool json = true,
        bool isInteractive = false,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        IRedirectCapture? httpCapture = null,
        CancellationToken cancellationToken = default)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            ConsoleColorWriter.WriteError("Missing required --callback-url.", stderr);
            return ExitCodes.UsageError;
        }

        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var callbackUri))
        {
            ConsoleColorWriter.WriteError($"Invalid --callback-url: {callbackUrl}", stderr);
            return ExitCodes.UsageError;
        }

        if (!CaptureModeResolver.IsKnownModeToken(mode))
        {
            ConsoleColorWriter.WriteError(
                $"Unknown --mode '{mode}'. Expected auto|http|https|scheme|hosted.",
                stderr);
            return ExitCodes.UsageError;
        }

        if (timeoutSeconds <= 0)
        {
            ConsoleColorWriter.WriteError("--timeout must be a positive number of seconds.", stderr);
            return ExitCodes.UsageError;
        }

        var resolvedMode = CaptureModeResolver.Resolve(callbackUri, mode, relayBase);
        if (resolvedMode is null)
        {
            ConsoleColorWriter.WriteError(
                $"Unsupported callback URL for capture: {callbackUrl}. Use loopback http(s), a custom scheme, or the hosted relay URL.",
                stderr);
            return ExitCodes.UsageError;
        }

        if (resolvedMode == CaptureMode.HttpsLoopback
            && !CertMaterial.HasExplicitMaterial(certPath, certPemPath, certKeyPath))
        {
            ConsoleColorWriter.WriteError(
                "HTTPS loopback requires --cert <pfx> or --cert-pem + --cert-key.",
                stderr);
            return ExitCodes.UsageError;
        }

        if (resolvedMode is CaptureMode.HttpsLoopback or CaptureMode.CustomScheme or CaptureMode.Hosted)
        {
            // Soft-stub until US2–US4: validation passes for HTTPS flags above; other modes deferred.
            if (resolvedMode != CaptureMode.HttpsLoopback)
            {
                ConsoleColorWriter.WriteError(
                    $"Capture mode {resolvedMode} is not implemented in this build yet.",
                    stderr);
                return ExitCodes.UsageError;
            }

            // HTTPS: cert material load validates non-TTY early; listen comes in US2.
            try
            {
                _ = CertMaterial.Load(
                    certPath, certPassword, certPemPath, certKeyPath, isInteractive, stderr);
            }
            catch (InvalidOperationException ex)
            {
                ConsoleColorWriter.WriteError(ex.Message, stderr);
                return ExitCodes.UsageError;
            }

            ConsoleColorWriter.WriteError(
                "HTTPS loopback listen is not implemented in this build yet (US2).",
                stderr);
            return ExitCodes.UsageError;
        }

        var options = new CaptureOptions(
            CallbackUrl: callbackUri,
            Mode: resolvedMode,
            Timeout: TimeSpan.FromSeconds(timeoutSeconds),
            CertPath: certPath,
            CertPassword: certPassword,
            CertPemPath: certPemPath,
            CertKeyPath: certKeyPath,
            RelayBaseUrl: relayBase,
            State: state);

        ConsoleColorWriter.WriteInfo($"Listening for OAuth callback on {callbackUri} …", stderr);

        var capture = httpCapture ?? new LoopbackHttpCapture();
        CaptureResult result;
        try
        {
            result = await capture.CaptureAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }

        if (json)
        {
            var payload = JsonSerializer.Serialize(result, CaptureResultJsonContext.Default.CaptureResult);
            stdout.WriteLine(payload);
        }

        if (result.Ok)
        {
            return ExitCodes.Success;
        }

        if (string.Equals(result.Error, "timeout", StringComparison.OrdinalIgnoreCase))
        {
            return ExitCodes.CaptureTimeout;
        }

        // IdP error= on redirect (or state mismatch treated as OAuth-ish failure)
        if (!string.IsNullOrEmpty(result.Error))
        {
            return ExitCodes.OAuthRedirectError;
        }

        return ExitCodes.CaptureTimeout;
    }
}
