using Api2Skill.Auth;

namespace Api2Skill.Model;

/// <summary>
/// Re-runs <see cref="AttachmentResolver"/> against an already-built <see cref="SkillModel"/>
/// (e.g. when baking scaffolded profiles onto a composed model) and returns a model with
/// per-operation <c>AuthProfileNames</c> updated.
/// </summary>
public static class SkillModelAuthApplier
{
    public static SkillModel Apply(SkillModel model, AuthConfig authConfig)
    {
        var operationTags = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var operationSchemeIds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        foreach (var tag in model.Tags)
        {
            foreach (var op in tag.Operations)
            {
                if (!operationTags.TryGetValue(op.OperationId, out var tags))
                {
                    operationTags[op.OperationId] = [tag.Tag];
                }
                else if (!tags.Contains(tag.Tag, StringComparer.Ordinal))
                {
                    operationTags[op.OperationId] = [.. tags, tag.Tag];
                }

                operationSchemeIds[op.OperationId] = op.SecuritySchemeIds;
            }
        }

        var knownSchemeIds = model.SecuritySchemes
            .Select(s => s.Id)
            .Concat(operationSchemeIds.Values.SelectMany(ids => ids))
            .ToHashSet(StringComparer.Ordinal);

        var resolved = AttachmentResolver.Resolve(authConfig, operationTags, operationSchemeIds, knownSchemeIds);

        var baked = new Dictionary<string, OperationModel>(StringComparer.Ordinal);
        var newTags = model.Tags.Select(tag =>
        {
            var ops = tag.Operations.Select(op =>
            {
                if (!baked.TryGetValue(op.OperationId, out var rewritten))
                {
                    rewritten = resolved.ProfileNamesByOperationId.TryGetValue(op.OperationId, out var names)
                        ? op with { AuthProfileNames = names }
                        : op with { AuthProfileNames = [] };
                    baked[op.OperationId] = rewritten;
                }

                return rewritten;
            }).ToList();
            return tag with { Operations = ops };
        }).ToList();

        return model with
        {
            Tags = newTags,
            AuthConfig = authConfig,
            Warnings = [.. model.Warnings, .. resolved.Warnings],
        };
    }
}
