using Api2Skill.Auth;
using Api2Skill.Model;
using Api2Skill.Parsing;

namespace Api2Skill.Tests.Model;

/// <summary>
/// T045/T046 (US2): confirms <see cref="Api2Skill.Auth.AttachmentResolver"/>'s output is fully
/// wired through <see cref="SkillModelBuilder"/> — not just correct in resolver-level unit tests
/// (Auth/AttachmentResolverTests.cs) — covering 3+ stacked profiles on one operation and warnings
/// actually landing in <see cref="SkillModel.Warnings"/>, which is what
/// <c>GenerateCommand.RunAsync</c> prints (the "surfaced through console output" requirement).
/// </summary>
public class AuthAttachmentIntegrationTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static async Task<Microsoft.OpenApi.OpenApiDocument> LoadPetstoreDocumentAsync()
    {
        await using var stream = new MemoryStream(await File.ReadAllBytesAsync(FixturePath("petstore.json")));
        var loaded = await OpenApiLoader.LoadAsync(stream, "json");
        return loaded.Document;
    }

    [Fact]
    public async Task UnusedTagAttachment_WarningReachesModelWarnings_WhichGenerateCommandPrints()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var authConfig = new AuthConfig([
            new AuthProfile("ghost", AuthType.Bearer, new Attachment(AttachScope.Tags, ["NoSuchTag"]),
                new BearerSettings("{secret:T}"), null, null, null, null),
        ]);

        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        Assert.Contains(model.Warnings, w => w.Contains("NoSuchTag", StringComparison.Ordinal) && w.Contains("ghost", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ThreeStackedProfiles_AllApplyToOneOperation_ViaSkillModelBuilder()
    {
        var doc = await LoadPetstoreDocumentAsync();
        var authConfig = new AuthConfig([
            new AuthProfile("a", AuthType.Bearer, Attachment.Global, new BearerSettings("{secret:A}"), null, null, null, null),
            new AuthProfile("b", AuthType.Custom, Attachment.Global, null, null,
                new CustomSettings([new HeaderEntry("ApiKey", "{secret:B}")]), null, null),
            new AuthProfile("c", AuthType.Custom, new Attachment(AttachScope.Tags, ["pet"]), null, null,
                new CustomSettings([new HeaderEntry("X-Trace", "{secret:C}")]), null, null),
        ]);

        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        var getPetById = model.Tags.SelectMany(t => t.Operations).First(o => o.OperationId == "getPetById");
        Assert.Equal(["a", "b", "c"], getPetById.AuthProfileNames);

        // A non-"pet" operation only gets the two global profiles, not "c".
        var storeOp = model.Tags.SelectMany(t => t.Operations).First(o => o.OperationId == "get_store_inventory");
        Assert.Equal(["a", "b"], storeOp.AuthProfileNames);
    }

    [Fact]
    public async Task SameOperationAcrossMultipleTagGroups_GetsIdenticalAuthProfileNamesEverywhere()
    {
        // Regression guard: SkillModelBuilder must resolve each operation's AuthProfileNames
        // exactly ONCE and reuse that instance everywhere the operation appears (an operation
        // can be listed under more than one TagGroup) — otherwise two occurrences could
        // disagree about which profiles apply.
        var doc = await LoadPetstoreDocumentAsync();
        var authConfig = new AuthConfig([
            new AuthProfile("g", AuthType.Bearer, Attachment.Global, new BearerSettings("{secret:G}"), null, null, null, null),
        ]);

        var model = SkillModelBuilder.Build(doc, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0,
            new BuildOptions(Name: "petstore", AuthConfig: authConfig));

        var occurrences = model.Tags.SelectMany(t => t.Operations).Where(o => o.OperationId == "getPetById").ToList();
        Assert.All(occurrences, o => Assert.Equal(["g"], o.AuthProfileNames));
    }
}
