using System.CommandLine;
using Api2Skill.Auth;

namespace Api2Skill.Cli;

/// <summary>
/// <c>api2skill login --skill</c> — end-to-end interactive OAuth login for a skill directory.
/// Cert/timeout/relay flags mirror <see cref="OAuthCaptureCommand"/> and are forwarded to capture.
/// </summary>
public static class LoginCommand
{
    public static Command Create()
    {
        var skillOption = new Option<string>("--skill")
        {
            Description = "Skill root directory (contains auth.json).",
            Required = true,
        };
        var profileOption = new Option<string?>("--profile")
        {
            Description = "OAuth2 authorization_code profile name (default: sole auth-code profile).",
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

        var command = new Command("login", "Interactive OAuth login for a generated skill (--skill)")
        {
            skillOption,
            profileOption,
            modeOption,
            timeoutOption,
            certOption,
            certPasswordOption,
            certPemOption,
            certKeyOption,
            relayBaseOption,
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var exit = await RunAsync(
                skillDir: parseResult.GetValue(skillOption),
                profile: parseResult.GetValue(profileOption),
                mode: parseResult.GetValue(modeOption),
                timeoutSeconds: parseResult.GetValue(timeoutOption),
                certPath: parseResult.GetValue(certOption),
                certPassword: parseResult.GetValue(certPasswordOption),
                certPemPath: parseResult.GetValue(certPemOption),
                certKeyPath: parseResult.GetValue(certKeyOption),
                relayBase: parseResult.GetValue(relayBaseOption),
                isInteractive: !Console.IsInputRedirected,
                stdout: Console.Out,
                stderr: Console.Error,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return exit;
        });

        return command;
    }

    /// <summary>Testable entry point for <c>login</c>.</summary>
    public static Task<int> RunAsync(
        string? skillDir,
        string? profile = null,
        string? mode = "auto",
        int timeoutSeconds = 180,
        string? certPath = null,
        string? certPassword = null,
        string? certPemPath = null,
        string? certKeyPath = null,
        string? relayBase = null,
        bool isInteractive = true,
        SkillOAuthLogin.Hooks? hooks = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        CancellationToken cancellationToken = default)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (string.IsNullOrWhiteSpace(skillDir))
        {
            ConsoleColorWriter.WriteError("Missing required --skill.", stderr);
            return Task.FromResult(ExitCodes.UsageError);
        }

        return SkillOAuthLogin.RunAsync(
            new SkillOAuthLogin.RunOptions(
                SkillDir: skillDir,
                ProfileName: profile,
                TimeoutSeconds: timeoutSeconds,
                CertPath: certPath,
                CertPassword: certPassword,
                CertPemPath: certPemPath,
                CertKeyPath: certKeyPath,
                RelayBase: relayBase,
                Mode: mode,
                IsInteractive: isInteractive),
            hooks,
            stdout,
            stderr,
            cancellationToken);
    }
}
