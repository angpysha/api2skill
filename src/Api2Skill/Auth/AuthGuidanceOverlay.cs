using System.Text.Json;
using System.Text.Json.Nodes;
using Api2Skill.Model;

namespace Api2Skill.Auth;

/// <summary>
/// Overlays composed-model <c>_guidance.schemes</c> onto an existing <c>auth.json</c> so merge
/// keeps first-wins profiles while documenting every final (possibly prefixed) scheme id.
/// </summary>
public static class AuthGuidanceOverlay
{
    /// <summary>
    /// Returns JSON with profiles (and other user keys) preserved, but <c>_guidance</c> replaced
    /// from <paramref name="scaffoldJson"/> produced by <see cref="AuthScaffold.Build"/>.
    /// When <paramref name="existingAuthJson"/> is null, returns <paramref name="scaffoldJson"/>.
    /// </summary>
    public static string Apply(string? existingAuthJson, string scaffoldJson)
    {
        if (string.IsNullOrWhiteSpace(existingAuthJson))
        {
            return scaffoldJson;
        }

        JsonNode? existingNode;
        JsonNode? scaffoldNode;
        try
        {
            existingNode = JsonNode.Parse(existingAuthJson);
            scaffoldNode = JsonNode.Parse(scaffoldJson);
        }
        catch (JsonException)
        {
            return scaffoldJson;
        }

        if (existingNode is not JsonObject existingObj || scaffoldNode is not JsonObject scaffoldObj)
        {
            return scaffoldJson;
        }

        if (scaffoldObj["_guidance"] is { } guidance)
        {
            existingObj["_guidance"] = guidance.DeepClone();
        }

        if (scaffoldObj["_tagAttachExamples"] is { } examples)
        {
            existingObj["_tagAttachExamples"] = examples.DeepClone();
        }
        else
        {
            existingObj.Remove("_tagAttachExamples");
        }

        return existingObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Builds scaffold guidance for <paramref name="model"/> and overlays it onto
    /// <paramref name="existingAuthJson"/> when present.
    /// </summary>
    public static string? RefreshForModel(SkillModel model, string? existingAuthJson)
    {
        if (model.SecuritySchemes.Count == 0)
        {
            return existingAuthJson;
        }

        var scaffold = AuthScaffold.Build(model);
        return Apply(existingAuthJson, scaffold.Json);
    }
}
