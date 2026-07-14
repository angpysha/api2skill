using System.CommandLine;
using Api2Skill.OAuth;

namespace Api2Skill.Cli;

/// <summary>
/// <c>register-protocol</c> / <c>unregister-protocol</c> — explicit first-party scheme
/// registration only (FR-009 / contracts/cli.md).
/// </summary>
public static class RegisterProtocolCommand
{
    public static Command CreateRegister()
    {
        var schemeOption = new Option<string>("--scheme")
        {
            Description = "URL scheme to register (default api2skill).",
            DefaultValueFactory = _ => ProtocolRegistration.DefaultScheme,
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite an existing registration.",
            DefaultValueFactory = _ => false,
        };

        var command = new Command(
            "register-protocol",
            "Register the api2skill URL scheme with the OS (explicit; never automatic).")
        {
            schemeOption,
            forceOption,
        };

        command.SetAction((parseResult, _) =>
        {
            var exit = RunRegister(
                scheme: parseResult.GetValue(schemeOption),
                force: parseResult.GetValue(forceOption));
            return Task.FromResult(exit);
        });

        return command;
    }

    public static Command CreateUnregister()
    {
        var schemeOption = new Option<string>("--scheme")
        {
            Description = "URL scheme to unregister (default api2skill).",
            DefaultValueFactory = _ => ProtocolRegistration.DefaultScheme,
        };

        var command = new Command(
            "unregister-protocol",
            "Remove a previously registered api2skill URL scheme from the OS.")
        {
            schemeOption,
        };

        command.SetAction((parseResult, _) =>
        {
            var exit = RunUnregister(scheme: parseResult.GetValue(schemeOption));
            return Task.FromResult(exit);
        });

        return command;
    }

    /// <summary>Testable register entry point.</summary>
    public static int RunRegister(
        string? scheme = null,
        bool force = false,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;
        scheme = string.IsNullOrWhiteSpace(scheme) ? ProtocolRegistration.DefaultScheme : scheme;

        try
        {
            ProtocolRegistration.Register(scheme, force);
            ConsoleColorWriter.WriteSuccess(
                $"Registered URL scheme '{scheme}'. IdP redirect URI example: {scheme}://oauth/callback",
                stderr);
            return ExitCodes.Success;
        }
        catch (PlatformNotSupportedException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (ArgumentException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError($"Protocol registration failed: {ex.Message}", stderr);
            return ExitCodes.AcquisitionFailure;
        }
    }

    /// <summary>Testable unregister entry point.</summary>
    public static int RunUnregister(
        string? scheme = null,
        TextWriter? stdout = null,
        TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;
        scheme = string.IsNullOrWhiteSpace(scheme) ? ProtocolRegistration.DefaultScheme : scheme;

        try
        {
            ProtocolRegistration.Unregister(scheme);
            ConsoleColorWriter.WriteSuccess($"Unregistered URL scheme '{scheme}'.", stderr);
            return ExitCodes.Success;
        }
        catch (PlatformNotSupportedException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (InvalidOperationException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (ArgumentException ex)
        {
            ConsoleColorWriter.WriteError(ex.Message, stderr);
            return ExitCodes.UsageError;
        }
        catch (Exception ex)
        {
            ConsoleColorWriter.WriteError($"Protocol unregistration failed: {ex.Message}", stderr);
            return ExitCodes.AcquisitionFailure;
        }
    }
}
