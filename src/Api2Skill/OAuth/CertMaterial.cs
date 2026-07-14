using System.Security.Cryptography.X509Certificates;
using Api2Skill.Cli;

namespace Api2Skill.OAuth;

/// <summary>Certificate material for local HTTPS loopback capture.</summary>
public enum CertKind
{
    Pfx,
    Pem,
}

/// <summary>
/// Loads PFX or PEM+key material and optionally prompts for a PFX password on a TTY
/// (contracts/cli.md — colored ask).
/// </summary>
public sealed class CertMaterial
{
    private CertMaterial(CertKind kind, X509Certificate2 certificate)
    {
        Kind = kind;
        Certificate = certificate;
    }

    public CertKind Kind { get; }

    public X509Certificate2 Certificate { get; }

    public static bool HasExplicitMaterial(string? certPath, string? certPemPath, string? certKeyPath) =>
        !string.IsNullOrWhiteSpace(certPath)
        || (!string.IsNullOrWhiteSpace(certPemPath) && !string.IsNullOrWhiteSpace(certKeyPath));

    /// <summary>
    /// Loads cert material. Throws <see cref="InvalidOperationException"/> with a user-facing message
    /// when required files are missing or the password cannot be obtained on a non-TTY.
    /// </summary>
    public static CertMaterial Load(
        string? certPath,
        string? certPassword,
        string? certPemPath,
        string? certKeyPath,
        bool isInteractive,
        TextWriter? stderr = null,
        Func<string?>? readPassword = null)
    {
        stderr ??= Console.Error;
        readPassword ??= static () =>
        {
            // Best-effort: password visible is acceptable for local cert unlock; echo off is OS-dependent.
            return Console.ReadLine();
        };

        if (!string.IsNullOrWhiteSpace(certPath))
        {
            if (!File.Exists(certPath))
            {
                throw new InvalidOperationException($"Certificate file not found: {certPath}");
            }

            var password = certPassword;
            if (string.IsNullOrEmpty(password))
            {
                // Try empty password first; if that fails and we're interactive, prompt.
                try
                {
                    var cert = X509CertificateLoader.LoadPkcs12FromFile(certPath, password: null);
                    return new CertMaterial(CertKind.Pfx, cert);
                }
                catch (Exception) when (isInteractive)
                {
                    ConsoleColorWriter.WriteWarning(
                        "PFX requires a password. Enter certificate password:",
                        stderr);
                    password = readPassword();
                    if (string.IsNullOrEmpty(password))
                    {
                        throw new InvalidOperationException("Certificate password is required for this PFX.");
                    }
                }
                catch (Exception ex) when (!isInteractive)
                {
                    throw new InvalidOperationException(
                        "HTTPS capture needs --cert-password (or an unencrypted PFX) when not attached to a TTY.",
                        ex);
                }
            }

            var loaded = X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
            return new CertMaterial(CertKind.Pfx, loaded);
        }

        if (!string.IsNullOrWhiteSpace(certPemPath) && !string.IsNullOrWhiteSpace(certKeyPath))
        {
            if (!File.Exists(certPemPath))
            {
                throw new InvalidOperationException($"Certificate PEM not found: {certPemPath}");
            }

            if (!File.Exists(certKeyPath))
            {
                throw new InvalidOperationException($"Certificate key not found: {certKeyPath}");
            }

            using var pemCert = X509Certificate2.CreateFromPemFile(certPemPath, certKeyPath);
            // Re-import via PKCS#12 so Kestrel can use the private key across platforms.
            var pfxBytes = pemCert.Export(X509ContentType.Pfx);
            var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, password: null);
            return new CertMaterial(CertKind.Pem, cert);
        }

        throw new InvalidOperationException(
            "HTTPS loopback requires --cert <pfx> or --cert-pem + --cert-key.");
    }
}
