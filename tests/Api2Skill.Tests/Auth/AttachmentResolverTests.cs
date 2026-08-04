using Api2Skill.Auth;

namespace Api2Skill.Tests.Auth;

/// <summary>
/// T025/T043/T044: <see cref="AttachmentResolver"/> — global/tag resolution, unused-tag warning
/// (FR-021), duplicate-header hard error (FR-021a).
/// </summary>
public class AttachmentResolverTests
{
    private static AuthProfile Bearer(string name, Attachment? attach = null) =>
        new(name, AuthType.Bearer, attach ?? Attachment.Global, new BearerSettings("{secret:T}"), null, null, null, null);

    private static AuthProfile Custom(string name, string headerName, Attachment? attach = null) =>
        new(name, AuthType.Custom, attach ?? Attachment.Global, null, null,
            new CustomSettings([new HeaderEntry(headerName, "{secret:V}")]), null, null);

    [Fact]
    public void Resolve_GlobalProfile_AppliesToEveryOperation()
    {
        var config = new AuthConfig([Bearer("g")]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>>
        {
            ["op1"] = ["TagA"],
            ["op2"] = ["TagB"],
        };

        var result = AttachmentResolver.Resolve(config, operationTags);

        Assert.Equal(["g"], result.ProfileNamesByOperationId["op1"]);
        Assert.Equal(["g"], result.ProfileNamesByOperationId["op2"]);
    }

    [Fact]
    public void Resolve_TagScopedProfile_AppliesOnlyToMatchingOperations()
    {
        var config = new AuthConfig([Bearer("admin", new Attachment(AttachScope.Tags, ["Admin"]))]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>>
        {
            ["adminOp"] = ["Admin"],
            ["publicOp"] = ["Public"],
        };

        var result = AttachmentResolver.Resolve(config, operationTags);

        Assert.Equal(["admin"], result.ProfileNamesByOperationId["adminOp"]);
        Assert.False(result.ProfileNamesByOperationId.ContainsKey("publicOp"));
    }

    [Fact]
    public void Resolve_GlobalAndTagScoped_BothApplyToMatchingOperation()
    {
        var config = new AuthConfig([
            Custom("gw", "ApiKey"),
            Bearer("admin", new Attachment(AttachScope.Tags, ["Admin"])),
        ]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["adminOp"] = ["Admin"] };

        var result = AttachmentResolver.Resolve(config, operationTags);

        Assert.Equal(["gw", "admin"], result.ProfileNamesByOperationId["adminOp"]);
    }

    [Fact]
    public void Resolve_TagAttachmentMatchingNoOperation_ProducesWarning_GenerationStillSucceeds()
    {
        var config = new AuthConfig([Bearer("orphan", new Attachment(AttachScope.Tags, ["Ghost"]))]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["Real"] };

        var result = AttachmentResolver.Resolve(config, operationTags);

        Assert.Contains(result.Warnings, w => w.Contains("Ghost", StringComparison.Ordinal));
        Assert.False(result.ProfileNamesByOperationId.ContainsKey("op1"));
    }

    [Fact]
    public void Resolve_TwoProfilesSameHeaderSameOperation_ThrowsCollisionException()
    {
        var config = new AuthConfig([Bearer("a"), Custom("b", "Authorization")]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["AnyTag"] };

        var ex = Assert.Throws<AuthConfigCollisionException>(() => AttachmentResolver.Resolve(config, operationTags));
        Assert.Contains("Authorization", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'a'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'b'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_TwoProfilesDifferentHeaders_NoCollision()
    {
        var config = new AuthConfig([Bearer("auth"), Custom("gw", "ApiKey")]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["AnyTag"] };

        var result = AttachmentResolver.Resolve(config, operationTags);

        Assert.Equal(["auth", "gw"], result.ProfileNamesByOperationId["op1"]);
    }

    [Fact]
    public void Resolve_HeaderNameCollision_IsCaseInsensitive()
    {
        var config = new AuthConfig([Custom("a", "authorization"), Custom("b", "Authorization")]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["AnyTag"] };

        Assert.Throws<AuthConfigCollisionException>(() => AttachmentResolver.Resolve(config, operationTags));
    }

    [Fact]
    public void Resolve_OperationWithNoApplicableProfile_IsAbsentFromResult()
    {
        var config = new AuthConfig([Bearer("admin", new Attachment(AttachScope.Tags, ["Admin"]))]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["publicOp"] = ["Public"] };

        var result = AttachmentResolver.Resolve(config, operationTags);

        Assert.Empty(result.ProfileNamesByOperationId);
    }

    [Fact]
    public void Resolve_SchemeNamedUnscopedProfiles_ApplyOnlyToMatchingOperations_NoCollision()
    {
        var config = new AuthConfig([
            new AuthProfile("oauth2", AuthType.OAuth2, Attachment.Global, null, null, null, null,
                new OAuthSettings(
                    OAuthGrant.AuthorizationCode, null, null, "https://auth.example/authorize",
                    "https://auth.example/token", [], "http://localhost:8400/callback", "auto",
                    ClientAuthMethod.Body, "cid", null, OAuthRequestExtras.Empty, OAuthRequestExtras.Empty, "access_token")),
            new AuthProfile("service", AuthType.Script, Attachment.Global, null, null, null,
                new ScriptSettings("echo token", "Authorization", BearerPrefix: true), null),
        ]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>>
        {
            ["userOp"] = ["Users"],
            ["svcOp"] = ["Internal"],
        };
        var operationSchemes = new Dictionary<string, IReadOnlyList<string>>
        {
            ["userOp"] = ["oauth2"],
            ["svcOp"] = ["service"],
        };

        var result = AttachmentResolver.Resolve(
            config, operationTags, operationSchemes, knownSchemeIds: new HashSet<string> { "oauth2", "service" });

        Assert.Equal(["oauth2"], result.ProfileNamesByOperationId["userOp"]);
        Assert.Equal(["service"], result.ProfileNamesByOperationId["svcOp"]);
    }

    [Fact]
    public void Resolve_TwoTrueGlobalAuthorizationProfiles_StillCollides()
    {
        var config = new AuthConfig([Bearer("interactive"), Bearer("batch")]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["AnyTag"] };
        var operationSchemes = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["api_key"] };

        var ex = Assert.Throws<AuthConfigCollisionException>(() =>
            AttachmentResolver.Resolve(
                config, operationTags, operationSchemes, knownSchemeIds: new HashSet<string> { "api_key" }));
        Assert.Contains("Authorization", ex.Message, StringComparison.Ordinal);
        Assert.Contains("api_key", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_UnusedProfile_ProducesWarning()
    {
        var config = new AuthConfig([Bearer("orphan-scheme")]);
        var operationTags = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["T"] };
        var operationSchemes = new Dictionary<string, IReadOnlyList<string>> { ["op1"] = ["other"] };

        var result = AttachmentResolver.Resolve(
            config, operationTags, operationSchemes, knownSchemeIds: new HashSet<string> { "other", "orphan-scheme" });

        Assert.Empty(result.ProfileNamesByOperationId);
        Assert.Contains(
            result.Warnings,
            w => w.Contains("orphan-scheme", StringComparison.Ordinal)
                 && w.Contains("never referenced", StringComparison.Ordinal));
    }
}
