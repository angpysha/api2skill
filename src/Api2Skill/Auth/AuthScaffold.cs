using System.Text.Json;
using System.Text.Json.Nodes;
using Api2Skill.Model;

namespace Api2Skill.Auth;

/// <summary>
/// Builds an inactive, secret-free <c>auth.json</c> template from a <see cref="SkillModel"/>'s
/// referenced security schemes (specs/006-auth-template-scaffold, research.md R3).
/// </summary>
public static class AuthScaffold
{
    private const string ActivationComment =
        "Edit profiles, then activate with: api2skill generate <spec> --auth-config ./auth.json --force";

    public static AuthScaffoldResult Build(SkillModel model)
    {
        if (model.SecuritySchemes.Count == 0)
        {
            throw new InvalidOperationException("AuthScaffold.Build requires at least one referenced security scheme.");
        }

        var schemeUsage = BuildSchemeUsage(model);
        var guidanceEntries = new List<SchemeGuidanceEntry>();
        var activeProfiles = new List<AuthProfile>();
        var manualOnlySchemes = new List<string>();

        foreach (var scheme in model.SecuritySchemes.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(scheme.Id))
            {
                throw new AuthConfigException("Every referenced security scheme must have a non-empty ID.");
            }

            schemeUsage.TryGetValue(scheme.Id, out var usage);
            var operationIds = usage?.OperationIds ?? [];
            var tags = usage?.Tags ?? [];

            var profile = TryBuildActiveProfile(scheme);
            var status = profile is null ? SchemeScaffoldStatus.ManualOnly : SchemeScaffoldStatus.Scaffolded;
            if (profile is not null)
            {
                activeProfiles.Add(profile);
            }
            else
            {
                manualOnlySchemes.Add(scheme.Id);
            }

            guidanceEntries.Add(new SchemeGuidanceEntry(
                SchemeId: scheme.Id,
                SuggestedProfileName: scheme.Id,
                Status: status,
                Kind: scheme.Kind,
                OperationIds: operationIds,
                Tags: tags));
        }

        var tagAttachExamples = BuildTagAttachExamples(model, activeProfiles);
        var guidance = new AuthScaffoldGuidance(guidanceEntries, tagAttachExamples);
        var json = Serialize(model, activeProfiles, guidance, manualOnlySchemes);
        return new AuthScaffoldResult(json, guidance);
    }

    private sealed record SchemeUsage(IReadOnlyList<string> OperationIds, IReadOnlyList<string> Tags);

    private static Dictionary<string, SchemeUsage> BuildSchemeUsage(SkillModel model)
    {
        var operationIdsByScheme = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var tagsByScheme = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var tagGroup in model.Tags)
        {
            foreach (var operation in tagGroup.Operations)
            {
                foreach (var schemeId in operation.SecuritySchemeIds)
                {
                    if (!operationIdsByScheme.TryGetValue(schemeId, out var ops))
                    {
                        ops = [];
                        operationIdsByScheme[schemeId] = ops;
                    }
                    if (!ops.Contains(operation.OperationId, StringComparer.Ordinal))
                    {
                        ops.Add(operation.OperationId);
                    }

                    if (!tagsByScheme.TryGetValue(schemeId, out var tags))
                    {
                        tags = new HashSet<string>(StringComparer.Ordinal);
                        tagsByScheme[schemeId] = tags;
                    }
                    tags.Add(tagGroup.Tag);
                }
            }
        }

        return operationIdsByScheme.ToDictionary(
            kvp => kvp.Key,
            kvp => new SchemeUsage(
                [.. kvp.Value.OrderBy(id => id, StringComparer.Ordinal)],
                [.. tagsByScheme[kvp.Key].OrderBy(t => t, StringComparer.Ordinal)]),
            StringComparer.Ordinal);
    }

    private static AuthProfile? TryBuildActiveProfile(SecuritySchemeModel scheme) => scheme.Kind switch
    {
        SecuritySchemeKind.Bearer => new AuthProfile(
            scheme.Id, AuthType.Bearer, Attachment.Global,
            Bearer: new BearerSettings($"{{secret:{scheme.Id}_TOKEN}}"),
            Basic: null, Custom: null, Script: null, OAuth: null),
        SecuritySchemeKind.Basic => new AuthProfile(
            scheme.Id, AuthType.Basic, Attachment.Global,
            Bearer: null,
            Basic: new BasicSettings($"{{secret:{scheme.Id}_USERNAME}}", $"{{secret:{scheme.Id}_PASSWORD}}"),
            Custom: null, Script: null, OAuth: null),
        SecuritySchemeKind.ApiKey when scheme.ApiKeyLocation == ApiKeyLocation.Header && scheme.ApiKeyName is { Length: > 0 } name =>
            new AuthProfile(
                scheme.Id, AuthType.Custom, Attachment.Global,
                Bearer: null, Basic: null,
                Custom: new CustomSettings([new HeaderEntry(name, $"{{secret:{scheme.Id}_APIKEY}}")]),
                Script: null, OAuth: null),
        SecuritySchemeKind.OAuth2 when scheme.OAuthTokenUrl is { Length: > 0 } tokenUrl => new AuthProfile(
            scheme.Id, AuthType.OAuth2, Attachment.Global,
            Bearer: null, Basic: null, Custom: null, Script: null,
            OAuth: new OAuthSettings(
                Grant: OAuthGrant.ClientCredentials,
                Preset: null,
                Tenant: null,
                AuthUrl: null,
                TokenUrl: tokenUrl,
                Scopes: scheme.OAuthScopes,
                CallbackUrl: "http://localhost:8400/callback",
                BrowserLaunch: "auto",
                ClientAuth: ClientAuthMethod.Body,
                ClientId: $"{{secret:{scheme.Id}_CLIENT_ID}}",
                ClientSecret: $"{{secret:{scheme.Id}_CLIENT_SECRET}}",
                AuthorizeRequest: OAuthRequestExtras.Empty,
                TokenRequest: OAuthRequestExtras.Empty,
                TokenField: "access_token")),
        _ => null,
    };

    private static IReadOnlyList<TagAttachExample> BuildTagAttachExamples(
        SkillModel model, IReadOnlyList<AuthProfile> activeProfiles)
    {
        if (activeProfiles.Count == 0)
        {
            return [];
        }

        var profileBySchemeId = activeProfiles.ToDictionary(p => p.Name, StringComparer.Ordinal);
        var examples = new List<TagAttachExample>();

        foreach (var tagGroup in model.Tags.OrderBy(t => t.Tag, StringComparer.Ordinal))
        {
            var schemeIds = tagGroup.Operations
                .SelectMany(o => o.SecuritySchemeIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (schemeIds.Count == 0)
            {
                continue;
            }

            var exampleProfiles = new List<AuthProfile>();
            foreach (var schemeId in schemeIds)
            {
                if (profileBySchemeId.TryGetValue(schemeId, out var profile))
                {
                    exampleProfiles.Add(profile with
                    {
                        Attach = new Attachment(AttachScope.Tags, [tagGroup.Tag]),
                    });
                }
            }

            if (exampleProfiles.Count > 0)
            {
                examples.Add(new TagAttachExample(tagGroup.Tag, schemeIds, exampleProfiles));
            }
        }

        return examples;
    }

    private static string Serialize(
        SkillModel model,
        IReadOnlyList<AuthProfile> activeProfiles,
        AuthScaffoldGuidance guidance,
        IReadOnlyList<string> manualOnlySchemes)
    {
        var profilesNode = JsonNode.Parse(AuthConfigLoader.Serialize(new AuthConfig(activeProfiles)))!;
        var root = new JsonObject
        {
            ["$comment"] = ActivationComment,
            ["profiles"] = profilesNode["profiles"]?.DeepClone(),
        };

        var schemesArray = new JsonArray();
        foreach (var entry in guidance.Schemes)
        {
            schemesArray.Add(new JsonObject
            {
                ["schemeId"] = entry.SchemeId,
                ["suggestedProfileName"] = entry.SuggestedProfileName,
                ["status"] = entry.Status == SchemeScaffoldStatus.Scaffolded ? "scaffolded" : "manualOnly",
                ["kind"] = entry.Kind.ToString(),
                ["operations"] = new JsonArray([.. entry.OperationIds.Select(id => (JsonNode)id)]),
                ["tags"] = new JsonArray([.. entry.Tags.Select(t => (JsonNode)t)]),
            });
        }

        root["_guidance"] = new JsonObject
        {
            ["schemes"] = schemesArray,
            ["manualOnlySchemes"] = new JsonArray([.. manualOnlySchemes.Select(id => (JsonNode)id)]),
        };

        if (guidance.TagAttachExamples.Count > 0)
        {
            var examplesArray = new JsonArray();
            foreach (var example in guidance.TagAttachExamples)
            {
                var profilesJson = JsonNode.Parse(AuthConfigLoader.Serialize(new AuthConfig(example.ExampleProfiles)))!;
                examplesArray.Add(new JsonObject
                {
                    ["tag"] = example.Tag,
                    ["schemeIds"] = new JsonArray([.. example.SchemeIds.Select(id => (JsonNode)id)]),
                    ["exampleProfiles"] = profilesJson["profiles"]?.DeepClone(),
                });
            }
            root["_tagAttachExamples"] = examplesArray;
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        return root.ToJsonString(options);
    }
}
