using System.Net.Http;
using Api2Skill.Model;

namespace Api2Skill.Tests.Model;

/// <summary>T007: SkillModelComposer — base URL, opId, scheme rename, tag collision, scriptKind.</summary>
public class SkillModelComposerTests
{
    [Fact]
    public void Compose_BaseUrlMismatch_Throws()
    {
        var a = MinimalModel("https://api.example.com", "opA", "api_key");
        var b = MinimalModel("https://other.example.com", "opB", "api_key");

        var ex = Assert.Throws<CompositionException>(() =>
            SkillModelComposer.Compose(
                "combined",
                [
                    (new SpecPartManifest("public", "./public.json"), a),
                    (new SpecPartManifest("admin", "./admin.json"), b),
                ]));

        Assert.Contains("Base URL mismatch", ex.Message, StringComparison.Ordinal);
        Assert.Contains("public=", ex.Message, StringComparison.Ordinal);
        Assert.Contains("admin=", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_BaseUrlOverride_AllowsMismatch()
    {
        var a = MinimalModel("https://api.example.com", "opA", "key_a");
        var b = MinimalModel("https://other.example.com", "opB", "key_b");

        var composed = SkillModelComposer.Compose(
            "combined",
            [
                (new SpecPartManifest("public", "./public.json"), a),
                (new SpecPartManifest("admin", "./admin.json"), b),
            ],
            new CompositionOptions(BaseUrlOverride: "https://api.example.com"));

        Assert.Equal("https://api.example.com", composed.BaseUrl);
        Assert.Equal(2, composed.SpecParts!.Count);
    }

    [Fact]
    public void Compose_OperationIdCollision_FailByDefault()
    {
        var a = MinimalModel("https://api.example.com", "sharedOp", "key_a");
        var b = MinimalModel("https://api.example.com", "sharedOp", "key_b");

        var ex = Assert.Throws<CompositionException>(() =>
            SkillModelComposer.Compose(
                "combined",
                [
                    (new SpecPartManifest("public", "./public.json"), a),
                    (new SpecPartManifest("admin", "./admin.json"), b),
                ]));

        Assert.Contains("operationId collision", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_OperationIdCollision_PrefixRenames()
    {
        var a = MinimalModel("https://api.example.com", "sharedOp", "key_a");
        var b = MinimalModel("https://api.example.com", "sharedOp", "key_b");

        var composed = SkillModelComposer.Compose(
            "combined",
            [
                (new SpecPartManifest("public", "./public.json"), a),
                (new SpecPartManifest("admin", "./admin.json"), b),
            ],
            new CompositionOptions(OnOperationIdConflict: OperationIdConflictMode.Prefix));

        var ids = composed.Tags.SelectMany(t => t.Operations).Select(o => o.OperationId).OrderBy(x => x).ToList();
        Assert.Contains("public__sharedOp", ids);
        Assert.Contains("admin__sharedOp", ids);
    }

    [Fact]
    public void Compose_SchemeCollision_RenamesAndRewritesOps()
    {
        var a = MinimalModel("https://api.example.com", "opA", "shared_key");
        var b = MinimalModel("https://api.example.com", "opB", "shared_key");

        var composed = SkillModelComposer.Compose(
            "combined",
            [
                (new SpecPartManifest("public", "./public.json"), a),
                (new SpecPartManifest("admin", "./admin.json"), b),
            ]);

        Assert.Contains(composed.SecuritySchemes, s => s.Id == "shared_key");
        Assert.Contains(composed.SecuritySchemes, s => s.Id == "admin_shared_key");

        var opB = composed.Tags.SelectMany(t => t.Operations).Single(o => o.OperationId == "opB");
        Assert.Equal(["admin_shared_key"], opB.SecuritySchemeIds);
    }

    [Fact]
    public void Compose_TagCollision_PrefixesDisplayTag()
    {
        var a = ModelWithTag("https://api.example.com", "users", "opA", "key_a");
        var b = ModelWithTag("https://api.example.com", "users", "opB", "key_b");

        var composed = SkillModelComposer.Compose(
            "combined",
            [
                (new SpecPartManifest("public", "./public.json"), a),
                (new SpecPartManifest("admin", "./admin.json"), b),
            ]);

        var tags = composed.Tags.Select(t => t.Tag).OrderBy(t => t).ToList();
        Assert.Contains("[public] users", tags);
        Assert.Contains("[admin] users", tags);
    }

    [Fact]
    public void Compose_SchemaNameCollision_NamespacesFilename()
    {
        var a = ModelWithSchema("https://api.example.com", "opA", "key_a", "User");
        var b = ModelWithSchema("https://api.example.com", "opB", "key_b", "User");

        var composed = SkillModelComposer.Compose(
            "combined",
            [
                (new SpecPartManifest("public", "./public.json"), a),
                (new SpecPartManifest("admin", "./admin.json"), b),
            ]);

        Assert.Contains(composed.ComponentSchemas!, s => s.Name == "User");
        Assert.Contains(composed.ComponentSchemas!, s => s.Name == "admin.User");
    }

    [Fact]
    public void ValidateScriptKinds_Mismatch_Throws()
    {
        var ex = Assert.Throws<CompositionException>(() =>
            SkillModelComposer.ValidateScriptKinds(
                canonicalScriptKind: null,
                [
                    ("public", "cs"),
                    ("admin", "fsx"),
                ]));

        Assert.Contains("scriptKind mismatch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_FileBaseUrlIgnored_PrefersHttpsFromOtherPart()
    {
        var a = MinimalModel("https://api.example.com", "opA", "key_a");
        var b = MinimalModel("file://", "opB", "key_b");

        var composed = SkillModelComposer.Compose(
            "combined",
            [
                (new SpecPartManifest("public", "./public.json"), a),
                (new SpecPartManifest("local", "./local.json"), b),
            ]);

        Assert.Equal("https://api.example.com", composed.BaseUrl);
    }

    [Fact]
    public void Compose_AllNonHttpBases_ThrowsAskingForBaseUrl()
    {
        var a = MinimalModel("file://", "opA", "key_a");
        var b = MinimalModel(null, "opB", "key_b");

        var ex = Assert.Throws<CompositionException>(() =>
            SkillModelComposer.Compose(
                "combined",
                [
                    (new SpecPartManifest("a", "./a.json"), a),
                    (new SpecPartManifest("b", "./b.json"), b),
                ]));

        Assert.Contains("No usable HTTP(S) base URL", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--base-url", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateScriptKinds_CanonicalFromTarget_RejectsDifferentSource()
    {
        var ex = Assert.Throws<CompositionException>(() =>
            SkillModelComposer.ValidateScriptKinds(
                canonicalScriptKind: "cs",
                [
                    ("public", "cs"),
                    ("admin", "fsx"),
                ]));

        Assert.Contains("expected 'cs'", ex.Message, StringComparison.Ordinal);
    }

    private static SkillModel MinimalModel(string? baseUrl, string operationId, string schemeId) =>
        ModelWithTag(baseUrl, "default", operationId, schemeId);

    private static SkillModel ModelWithTag(string? baseUrl, string tag, string operationId, string schemeId)
    {
        var op = new OperationModel(
            operationId,
            HttpMethod.Get,
            "/x",
            "summary",
            null,
            [],
            null,
            [schemeId],
            []);

        return new SkillModel(
            Name: "part",
            Title: "Part",
            Description: null,
            Version: "1.0.0",
            BaseUrl: baseUrl,
            SpecVersion: SpecVersionKind.OpenApi3_0,
            SecuritySchemes:
            [
                new SecuritySchemeModel(
                    schemeId,
                    SecuritySchemeKind.ApiKey,
                    "X-Api-Key",
                    ApiKeyLocation.Header,
                    null,
                    [],
                    [$"{schemeId}.apiKey"]),
            ],
            Tags: [new TagGroup(tag, null, [op])],
            Warnings: [],
            ComponentSchemas: []);
    }

    private static SkillModel ModelWithSchema(string? baseUrl, string operationId, string schemeId, string schemaName)
    {
        var model = ModelWithTag(baseUrl, "default", operationId, schemeId);
        return model with
        {
            ComponentSchemas = [new ComponentSchemaModel(schemaName, """{"type":"object"}""")],
        };
    }
}
