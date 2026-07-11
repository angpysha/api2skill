using Api2Skill.Auth;

namespace Api2Skill.Tests.Auth;

/// <summary>
/// T024/T048: <see cref="AuthConfigLoader"/> parse/validate — contracts/auth-config.md.
/// </summary>
public class AuthConfigLoaderTests
{
    [Fact]
    public void Parse_ValidBearerProfile_ProducesExpectedDomainModel()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "default", "type": "bearer", "token": "{secret:MY_TOKEN}" }
            ] }
            """);

        var profile = Assert.Single(config.Profiles);
        Assert.Equal("default", profile.Name);
        Assert.Equal(AuthType.Bearer, profile.Type);
        Assert.Equal(AttachScope.Global, profile.Attach.Scope);
        Assert.Equal("{secret:MY_TOKEN}", profile.Bearer!.Token);
    }

    [Fact]
    public void Parse_MalformedJson_ThrowsAuthConfigException()
    {
        var ex = Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("{ not valid json"));
        Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_EmptyProfilesArray_ThrowsAuthConfigException()
    {
        Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("""{ "profiles": [] }"""));
    }

    [Fact]
    public void Parse_UnknownProfileType_ThrowsAuthConfigException()
    {
        var ex = Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "x", "type": "kerberos" } ] }
            """));
        Assert.Contains("unknown type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_DuplicateProfileName_ThrowsAuthConfigException()
    {
        var ex = Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "a", "type": "bearer", "token": "{secret:T}" },
              { "name": "a", "type": "basic", "username": "{secret:U}", "password": "{secret:P}" }
            ] }
            """));
        Assert.Contains("Duplicate", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CustomProfileWithoutHeaders_ThrowsAuthConfigException()
    {
        Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("""
            { "profiles": [ { "name": "x", "type": "custom" } ] }
            """));
    }

    [Fact]
    public void Parse_CustomProfileWithMultipleHeaders_PreservesOrderAndAllEntries()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "gw", "type": "custom", "headers": [
                { "name": "Authorization", "value": "{secret:A}" },
                { "name": "ApiKey", "value": "{secret:K}" }
              ] }
            ] }
            """);

        var headers = config.Profiles[0].Custom!.Headers;
        Assert.Equal(2, headers.Count);
        Assert.Equal("Authorization", headers[0].Name);
        Assert.Equal("ApiKey", headers[1].Name);
    }

    [Fact]
    public void Parse_TagAttachment_RequiresNonEmptyTagsList()
    {
        Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "x", "type": "bearer", "token": "{secret:T}", "attach": { "scope": "tags" } }
            ] }
            """));
    }

    [Fact]
    public void Parse_TagAttachment_ParsesTagList()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "x", "type": "bearer", "token": "{secret:T}",
                "attach": { "scope": "tags", "tags": ["Admin", "Billing"] } }
            ] }
            """);

        var attach = config.Profiles[0].Attach;
        Assert.Equal(AttachScope.Tags, attach.Scope);
        Assert.Equal(["Admin", "Billing"], attach.Tags);
    }

    // --- oauth2 grant validation (T048/US3 groundwork) ---

    [Fact]
    public void Parse_OAuth2AuthorizationCode_RequiresAuthUrl()
    {
        var ex = Assert.Throws<AuthConfigException>(() => AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "aad", "type": "oauth2", "grant": "authorization_code",
                "clientId": "{secret:CID}", "tokenUrl": "https://example.com/token" }
            ] }
            """));
        Assert.Contains("authUrl", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_OAuth2ClientCredentials_RequiresTokenUrlOnly_NoAuthUrlNeeded()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "svc", "type": "oauth2", "grant": "client_credentials",
                "clientId": "{secret:CID}", "clientSecret": "{secret:CS}", "tokenUrl": "https://example.com/token" }
            ] }
            """);

        var oauth = config.Profiles[0].OAuth!;
        Assert.Equal(OAuthGrant.ClientCredentials, oauth.Grant);
        Assert.Equal("https://example.com/token", oauth.TokenUrl);
    }

    [Fact]
    public void Parse_OAuth2_DefaultsToBodyClientAuthAndDefaultCallback()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "svc", "type": "oauth2", "grant": "client_credentials",
                "clientId": "{secret:CID}", "tokenUrl": "https://example.com/token" }
            ] }
            """);

        var oauth = config.Profiles[0].OAuth!;
        Assert.Equal(ClientAuthMethod.Body, oauth.ClientAuth);
        Assert.Equal("http://localhost:8400/callback", oauth.CallbackUrl);
    }

    [Fact]
    public void Parse_OAuth2ClientSecret_IsOptional()
    {
        var config = AuthConfigLoader.Parse("""
            { "profiles": [
              { "name": "pub", "type": "oauth2", "authUrl": "https://example.com/authorize",
                "tokenUrl": "https://example.com/token", "clientId": "{secret:CID}" }
            ] }
            """);

        Assert.Null(config.Profiles[0].OAuth!.ClientSecret);
    }

    // --- --auth shorthand ---

    [Theory]
    [InlineData("bearer")]
    [InlineData("basic")]
    [InlineData("custom")]
    public void CreateShorthand_StructureFreeTypes_ProducesOneGlobalProfile(string type)
    {
        var config = AuthConfigLoader.CreateShorthand(type);
        var profile = Assert.Single(config.Profiles);
        Assert.Equal(AttachScope.Global, profile.Attach.Scope);
    }

    [Theory]
    [InlineData("oauth2")]
    [InlineData("entra")]
    public void CreateShorthand_InteractiveTypes_ThrowsAuthShorthandUnsupportedException(string type)
    {
        var ex = Assert.Throws<AuthShorthandUnsupportedException>(() => AuthConfigLoader.CreateShorthand(type));
        Assert.Contains("--auth-config", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadFromFile_MissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Throws<FileNotFoundException>(() => AuthConfigLoader.LoadFromFile(path, out _));
    }

    [Fact]
    public void LoadFromFile_ReturnsRawJsonVerbatim()
    {
        var path = Path.Combine(Path.GetTempPath(), "auth-" + Guid.NewGuid().ToString("N") + ".json");
        const string raw = """
            {
              "profiles": [ { "name": "default", "type": "bearer", "token": "{secret:T}" } ]
            }
            """;
        File.WriteAllText(path, raw);
        try
        {
            AuthConfigLoader.LoadFromFile(path, out var rawJson);
            Assert.Equal(raw, rawJson);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Serialize_RoundTripsBearerProfile()
    {
        var config = AuthConfigLoader.CreateShorthand("bearer");
        var json = AuthConfigLoader.Serialize(config);
        var reparsed = AuthConfigLoader.Parse(json);

        Assert.Equal(config.Profiles[0].Name, reparsed.Profiles[0].Name);
        Assert.Equal(config.Profiles[0].Bearer!.Token, reparsed.Profiles[0].Bearer!.Token);
    }
}
