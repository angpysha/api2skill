using Api2Skill.Auth;

namespace Api2Skill.Tests.Auth;

/// <summary>T026: <see cref="EntraPreset"/> expansion — research.md R5, FR-015.</summary>
public class EntraPresetTests
{
    [Fact]
    public void Expand_WithTenant_FillsAuthorizeAndTokenUrls()
    {
        var (authUrl, tokenUrl, _) = EntraPreset.Expand("entra", "contoso.onmicrosoft.com", null, null, []);

        Assert.Equal("https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/authorize", authUrl);
        Assert.Equal("https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/token", tokenUrl);
    }

    [Fact]
    public void Expand_AddsOfflineAccessScope_WhenNotAlreadyPresent()
    {
        var (_, _, scopes) = EntraPreset.Expand("entra", "tenant-id", null, null, ["api://app/.default"]);

        Assert.Contains("offline_access", scopes);
        Assert.Contains("api://app/.default", scopes);
    }

    [Fact]
    public void Expand_DoesNotDuplicateOfflineAccessScope_WhenAlreadyPresent()
    {
        var (_, _, scopes) = EntraPreset.Expand("entra", "tenant-id", null, null, ["offline_access"]);

        Assert.Single(scopes, s => s == "offline_access");
    }

    [Fact]
    public void Expand_ExplicitAuthUrl_OverridesPresetExpansion()
    {
        var (authUrl, _, _) = EntraPreset.Expand("entra", "tenant-id", "https://custom.example.com/authorize", null, []);

        Assert.Equal("https://custom.example.com/authorize", authUrl);
    }

    [Fact]
    public void Expand_NoPreset_PassesExplicitValuesThrough()
    {
        var (authUrl, tokenUrl, scopes) = EntraPreset.Expand(
            null, null, "https://example.com/authorize", "https://example.com/token", ["read"]);

        Assert.Equal("https://example.com/authorize", authUrl);
        Assert.Equal("https://example.com/token", tokenUrl);
        Assert.Equal(["read"], scopes);
    }

    [Fact]
    public void Expand_EntraPresetWithoutTenantOrExplicitUrls_ProducesNullUrls()
    {
        var (authUrl, tokenUrl, _) = EntraPreset.Expand("entra", null, null, null, []);

        Assert.Null(authUrl);
        Assert.Null(tokenUrl);
    }
}
