using Api2Skill.Auth;

namespace Api2Skill.Tests.Auth;

/// <summary>T027: <see cref="SecretRefScanner"/> — FR-007/FR-008.</summary>
public class SecretRefScannerTests
{
    [Fact]
    public void Scan_BearerProfile_FindsTokenReference()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "b", "type": "bearer", "token": "{secret:MY_TOKEN}" } ] }
            """);

        Assert.Equal(["MY_TOKEN"], SecretRefScanner.Scan(config));
    }

    [Fact]
    public void Scan_BasicProfile_FindsUsernameAndPassword()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "b", "type": "basic", "username": "{secret:U}", "password": "{secret:P}" } ] }
            """);

        Assert.Equal(["P", "U"], SecretRefScanner.Scan(config)); // sorted ordinal
    }

    [Fact]
    public void Scan_CustomProfile_FindsAllHeaderValueReferences()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "gw", "type": "custom", "headers": [
              { "name": "Authorization", "value": "{secret:A}" },
              { "name": "ApiKey", "value": "{secret:K}" }
            ] } ] }
            """);

        Assert.Equal(["A", "K"], SecretRefScanner.Scan(config));
    }

    [Fact]
    public void Scan_CustomProfile_IgnoresLiteralValues()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "gw", "type": "custom", "headers": [
              { "name": "X-Static", "value": "not-a-secret" }
            ] } ] }
            """);

        Assert.Empty(SecretRefScanner.Scan(config));
    }

    [Fact]
    public void Scan_OAuth2Profile_FindsClientIdAndClientSecret()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "o", "type": "oauth2", "grant": "client_credentials",
              "tokenUrl": "https://example.com/token",
              "clientId": "{secret:CID}", "clientSecret": "{secret:CS}" } ] }
            """);

        Assert.Equal(["CID", "CS"], SecretRefScanner.Scan(config));
    }

    [Fact]
    public void Scan_ScriptProfile_CommandIsNotTreatedAsSecretHost()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "s", "type": "script", "command": "echo {secret:NOT_REALLY}" } ] }
            """);

        Assert.Empty(SecretRefScanner.Scan(config));
    }

    [Fact]
    public void Scan_DuplicateReferencesAcrossProfiles_AreDeduplicated()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "a", "type": "bearer", "token": "{secret:SHARED}" },
              { "name": "b", "type": "custom", "headers": [ { "name": "X", "value": "{secret:SHARED}" } ] }
            ] }
            """);

        Assert.Equal(["SHARED"], SecretRefScanner.Scan(config));
    }
}
