using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;

namespace Api2Skill.OAuth;

/// <summary>
/// Explicit OS protocol-handler registration for the first-party custom scheme (FR-009).
/// Best-effort: macOS LaunchServices app-bundle shim, Windows HKCU URL protocol, Linux
/// <c>xdg-mime</c> desktop entry. Never registers silently on install/login.
/// </summary>
public static class ProtocolRegistration
{
    public const string DefaultScheme = "api2skill";

    public static bool IsSupportedPlatform =>
        OperatingSystem.IsWindows()
        || OperatingSystem.IsMacOS()
        || OperatingSystem.IsLinux();

    public static bool IsFirstPartyScheme(string scheme) =>
        !string.IsNullOrWhiteSpace(scheme)
        && scheme.Equals(DefaultScheme, StringComparison.OrdinalIgnoreCase);

    public static string ResolveHandlerCommand(string? executableOverride = null)
    {
        var processPath = executableOverride
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the api2skill executable path.");

        var entry = Assembly.GetEntryAssembly()?.Location;
        var fileName = Path.GetFileNameWithoutExtension(processPath);

        if (!string.IsNullOrEmpty(entry)
            && entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && (fileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                || fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)))
        {
            return Quote(processPath) + " " + Quote(entry) + " " + Placeholder(OperatingSystem.IsWindows());
        }

        return Quote(processPath) + " " + Placeholder(OperatingSystem.IsWindows());
    }

    public static bool IsRegistered(string scheme)
    {
        scheme = NormalizeScheme(scheme);
        if (OperatingSystem.IsWindows())
        {
            return WindowsIsRegistered(scheme);
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacIsRegistered(scheme);
        }

        if (OperatingSystem.IsLinux())
        {
            return LinuxIsRegistered(scheme);
        }

        return false;
    }

    public static void Register(string scheme, bool force = false, string? handlerCommand = null)
    {
        EnsureSupported();
        scheme = NormalizeScheme(scheme);
        ValidateSchemeName(scheme);

        if (IsRegistered(scheme) && !force)
        {
            throw new InvalidOperationException(
                $"Scheme '{scheme}' is already registered. Re-run with --force to overwrite, or unregister-protocol first.");
        }

        var command = handlerCommand ?? ResolveHandlerCommand();

        if (OperatingSystem.IsWindows())
        {
            WindowsRegister(scheme, command);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            MacRegister(scheme, command);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            LinuxRegister(scheme, command);
            return;
        }

        throw Unsupported();
    }

    public static void Unregister(string scheme)
    {
        EnsureSupported();
        scheme = NormalizeScheme(scheme);
        ValidateSchemeName(scheme);

        if (OperatingSystem.IsWindows())
        {
            WindowsUnregister(scheme);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            MacUnregister(scheme);
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            LinuxUnregister(scheme);
            return;
        }

        throw Unsupported();
    }

    private static void EnsureSupported()
    {
        if (!IsSupportedPlatform)
        {
            throw Unsupported();
        }
    }

    private static PlatformNotSupportedException Unsupported() =>
        new(
            "Custom URL scheme registration is not supported on this OS. " +
            "Use `api2skill register-protocol` on macOS, Windows, or Linux.");

    private static string NormalizeScheme(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            throw new ArgumentException("Scheme name is required.", nameof(scheme));
        }

        scheme = scheme.Trim();
        if (scheme.EndsWith("://", StringComparison.Ordinal))
        {
            scheme = scheme[..^3];
        }

        var colon = scheme.IndexOf(':');
        if (colon >= 0)
        {
            scheme = scheme[..colon];
        }

        return scheme.ToLowerInvariant();
    }

    private static void ValidateSchemeName(string scheme)
    {
        if (scheme is "http" or "https" or "file" or "ftp")
        {
            throw new InvalidOperationException($"Refusing to register reserved scheme '{scheme}'.");
        }

        foreach (var c in scheme)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not '+' and not '-' and not '.')
            {
                throw new InvalidOperationException($"Invalid scheme name '{scheme}'.");
            }
        }
    }

    private static string Quote(string path) =>
        path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;

    private static string Placeholder(bool windows) => windows ? "\"%1\"" : "\"%u\"";

    [SupportedOSPlatform("windows")]
    private static bool WindowsIsRegistered(string scheme)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + scheme);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsRegister(string scheme, string command)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + scheme);
        key.SetValue(string.Empty, $"URL:{scheme} Protocol");
        key.SetValue("URL Protocol", string.Empty);
        using var commandKey = key.CreateSubKey(@"shell\open\command");
        commandKey.SetValue(string.Empty, command);
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsUnregister(string scheme)
    {
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + scheme, throwOnMissingSubKey: false);
    }

    private static string MacAppBundlePath(string scheme) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Api2Skill",
            $"UrlHandler-{scheme}.app");

    private static bool MacIsRegistered(string scheme)
    {
        var plist = Path.Combine(MacAppBundlePath(scheme), "Contents", "Info.plist");
        return File.Exists(plist);
    }

    private static void MacRegister(string scheme, string command)
    {
        var app = MacAppBundlePath(scheme);
        var contents = Path.Combine(app, "Contents");
        var macOs = Path.Combine(contents, "MacOS");
        Directory.CreateDirectory(macOs);

        var handlerPath = Path.Combine(macOs, "handler");
        var script = new StringBuilder();
        script.AppendLine("#!/bin/bash");
        script.AppendLine("set -euo pipefail");
        script.AppendLine($"exec {command.Replace("\"%u\"", "\"$1\"", StringComparison.Ordinal)}");
        File.WriteAllText(handlerPath, script.ToString());
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                ArgumentList = { "+x", handlerPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            })?.WaitForExit(5_000);
        }
        catch
        {
            // best-effort
        }

        var plist = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>CFBundleExecutable</key>
              <string>handler</string>
              <key>CFBundleIdentifier</key>
              <string>dev.api2skill.urlhandler.{scheme}</string>
              <key>CFBundleName</key>
              <string>Api2Skill {scheme}</string>
              <key>CFBundlePackageType</key>
              <string>APPL</string>
              <key>CFBundleURLTypes</key>
              <array>
                <dict>
                  <key>CFBundleURLName</key>
                  <string>Api2Skill {scheme} OAuth</string>
                  <key>CFBundleURLSchemes</key>
                  <array>
                    <string>{scheme}</string>
                  </array>
                </dict>
              </array>
            </dict>
            </plist>
            """;
        File.WriteAllText(Path.Combine(contents, "Info.plist"), plist);
        File.WriteAllText(Path.Combine(contents, "PkgInfo"), "APPL????");

        TryRunLsRegister(["-f", app]);
    }

    private static void MacUnregister(string scheme)
    {
        var app = MacAppBundlePath(scheme);
        if (Directory.Exists(app))
        {
            TryRunLsRegister(["-u", app]);
            try
            {
                Directory.Delete(app, recursive: true);
            }
            catch
            {
                // leave orphans if locked
            }
        }
    }

    private static void TryRunLsRegister(string[] args)
    {
        const string lsregister =
            "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister";
        if (!File.Exists(lsregister))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = lsregister,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var process = Process.Start(psi);
            process?.WaitForExit(10_000);
        }
        catch
        {
            // best-effort registration
        }
    }

    private static string LinuxDesktopPath(string scheme) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "applications",
            $"api2skill-{scheme}.desktop");

    private static bool LinuxIsRegistered(string scheme) => File.Exists(LinuxDesktopPath(scheme));

    private static void LinuxRegister(string scheme, string command)
    {
        var desktopPath = LinuxDesktopPath(scheme);
        Directory.CreateDirectory(Path.GetDirectoryName(desktopPath)!);

        var exec = command.Replace("\"%u\"", "%u", StringComparison.Ordinal);
        var content = $"""
            [Desktop Entry]
            Type=Application
            Name=Api2Skill {scheme} OAuth
            Exec={exec}
            StartupNotify=false
            MimeType=x-scheme-handler/{scheme};
            NoDisplay=true
            """;
        File.WriteAllText(desktopPath, content);

        TryRun("xdg-mime", ["default", Path.GetFileName(desktopPath), $"x-scheme-handler/{scheme}"]);
        TryRun("update-desktop-database", [Path.GetDirectoryName(desktopPath)!]);
    }

    private static void LinuxUnregister(string scheme)
    {
        var desktopPath = LinuxDesktopPath(scheme);
        if (File.Exists(desktopPath))
        {
            File.Delete(desktopPath);
        }

        TryRun("update-desktop-database", [Path.GetDirectoryName(desktopPath)!]);
    }

    private static void TryRun(string fileName, IEnumerable<string> args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var process = Process.Start(psi);
            process?.WaitForExit(10_000);
        }
        catch
        {
            // xdg tools optional
        }
    }
}
