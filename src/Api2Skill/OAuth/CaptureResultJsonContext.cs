using System.Text.Json.Serialization;

namespace Api2Skill.OAuth;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(CaptureResult))]
internal sealed partial class CaptureResultJsonContext : JsonSerializerContext;
