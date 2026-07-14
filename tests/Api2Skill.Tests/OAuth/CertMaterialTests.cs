using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

public class CertMaterialTests
{
    [Fact]
    public void HasExplicitMaterial_False_WhenNoFlags()
    {
        Assert.False(CertMaterial.HasExplicitMaterial(null, null, null));
        Assert.False(CertMaterial.HasExplicitMaterial("", null, null));
        Assert.False(CertMaterial.HasExplicitMaterial(null, "cert.pem", null));
        Assert.False(CertMaterial.HasExplicitMaterial(null, null, "key.pem"));
    }

    [Fact]
    public void HasExplicitMaterial_True_ForPfxOrPemPair()
    {
        Assert.True(CertMaterial.HasExplicitMaterial("/tmp/a.pfx", null, null));
        Assert.True(CertMaterial.HasExplicitMaterial(null, "/tmp/a.pem", "/tmp/a.key"));
    }

    [Fact]
    public void Load_MissingMaterial_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CertMaterial.Load(null, null, null, null, isInteractive: false, stderr: TextWriter.Null));
        Assert.Contains("--cert", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingPfxFile_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pfx");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CertMaterial.Load(path, "pass", null, null, isInteractive: false, stderr: TextWriter.Null));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_Pfx_WithPassword_Succeeds()
    {
        var (pfxPath, password) = WriteTempPfx();
        try
        {
            var material = CertMaterial.Load(pfxPath, password, null, null, isInteractive: false, stderr: TextWriter.Null);
            try
            {
                Assert.Equal(CertKind.Pfx, material.Kind);
                Assert.True(material.Certificate.HasPrivateKey);
            }
            finally { material.Certificate.Dispose(); }
        }
        finally { File.Delete(pfxPath); }
    }

    [Fact]
    public void Load_EncryptedPfx_NonInteractiveWithoutPassword_Throws()
    {
        var (pfxPath, _) = WriteTempPfx(password: "secret-pass");
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                CertMaterial.Load(pfxPath, null, null, null, isInteractive: false, stderr: TextWriter.Null));
            Assert.Contains("cert-password", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(pfxPath); }
    }

    [Fact]
    public void Load_PemPair_Succeeds()
    {
        var (pemPath, keyPath) = WriteTempPemPair();
        try
        {
            var material = CertMaterial.Load(null, null, pemPath, keyPath, isInteractive: false, stderr: TextWriter.Null);
            try
            {
                Assert.Equal(CertKind.Pem, material.Kind);
                Assert.True(material.Certificate.HasPrivateKey);
            }
            finally { material.Certificate.Dispose(); }
        }
        finally { File.Delete(pemPath); File.Delete(keyPath); }
    }

    [Fact]
    public void Load_InteractiveMissingPassword_UsesPromptCallback()
    {
        var (pfxPath, password) = WriteTempPfx(password: "prompted");
        try
        {
            var material = CertMaterial.Load(pfxPath, null, null, null, true, TextWriter.Null, () => password);
            try
            {
                Assert.Equal(CertKind.Pfx, material.Kind);
                Assert.True(material.Certificate.HasPrivateKey);
            }
            finally { material.Certificate.Dispose(); }
        }
        finally { File.Delete(pfxPath); }
    }

    internal static (string PfxPath, string Password) WriteTempPfx(string password = "test-pass")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        AddLoopbackSan(req);
        using var created = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(14));
        var path = Path.Combine(Path.GetTempPath(), $"api2skill-test-{Guid.NewGuid():N}.pfx");
        File.WriteAllBytes(path, created.Export(X509ContentType.Pfx, password));
        return (path, password);
    }

    internal static (string PemPath, string KeyPath) WriteTempPemPair()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        AddLoopbackSan(req);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(14));
        var pemPath = Path.Combine(Path.GetTempPath(), $"api2skill-test-{Guid.NewGuid():N}.pem");
        var keyPath = Path.Combine(Path.GetTempPath(), $"api2skill-test-{Guid.NewGuid():N}.key");
        File.WriteAllText(pemPath, cert.ExportCertificatePem());
        File.WriteAllText(keyPath, rsa.ExportPkcs8PrivateKeyPem());
        return (pemPath, keyPath);
    }

    internal static X509Certificate2 CreateSelfSigned(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        AddLoopbackSan(req);
        using var created = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(14));
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx, "reimport"), "reimport");
    }

    private static void AddLoopbackSan(CertificateRequest req)
    {
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(System.Net.IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    }
}
