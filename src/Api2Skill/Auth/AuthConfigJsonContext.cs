using System.Text.Json.Serialization;

namespace Api2Skill.Auth;

/// <summary>
/// Source-generated (reflection-free) <see cref="JsonSerializerContext"/> for <c>auth.json</c>
/// parsing — research.md R9. <c>auth.json</c> uses camelCase keys (contracts/auth-config.md);
/// this maps them onto the PascalCase DTO properties without per-property attributes.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(AuthConfigDto))]
internal sealed partial class AuthConfigJsonContext : JsonSerializerContext;
