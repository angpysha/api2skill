namespace Api2Skill.OAuth;

/// <summary>
/// Stdout JSON for <c>oauth-capture</c> — field names match
/// <c>contracts/oauth-capture.md</c> (camelCase).
/// </summary>
public sealed record CaptureResult(
    bool Ok,
    string Mode,
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription,
    string CallbackUrl);
