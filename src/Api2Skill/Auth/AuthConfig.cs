namespace Api2Skill.Auth;

/// <summary>
/// The validated, in-memory form of a committed <c>auth.json</c> (contracts/auth-config.md) —
/// the root the generator validates against and copies verbatim into the skill directory.
/// </summary>
public sealed record AuthConfig(IReadOnlyList<AuthProfile> Profiles);
