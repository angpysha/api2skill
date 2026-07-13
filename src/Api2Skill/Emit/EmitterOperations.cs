using Api2Skill.Model;

namespace Api2Skill.Emit;

/// <summary>
/// Shared helpers for emitting dispatcher operation tables from a <see cref="SkillModel"/>.
/// </summary>
internal static class EmitterOperations
{
    /// <summary>
    /// Returns one row per callable operation. An operation listed under multiple tags appears
    /// once — keyed by <see cref="OperationModel.OperationId"/>, not path (the same path can
    /// host different HTTP methods).
    /// </summary>
    internal static IEnumerable<OperationModel> DistinctByOperationId(SkillModel model) =>
        model.Tags
            .SelectMany(t => t.Operations)
            .GroupBy(o => o.OperationId, StringComparer.Ordinal)
            .Select(g => g.First());
}
