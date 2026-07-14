using System.Text.Json.Serialization;

namespace Api2Skill.OAuth;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HostedSessionCreateRequest))]
[JsonSerializable(typeof(HostedSessionCreateResponse))]
[JsonSerializable(typeof(HostedPollResponse))]
internal sealed partial class HostedRelayJsonContext : JsonSerializerContext;
