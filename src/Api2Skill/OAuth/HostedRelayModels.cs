namespace Api2Skill.OAuth;

/// <summary>POST /v1/session request body (<c>contracts/hosted-relay.md</c>).</summary>
public sealed record HostedSessionCreateRequest(string? State, int? TtlSeconds);

/// <summary>POST /v1/session 201 response.</summary>
public sealed record HostedSessionCreateResponse(string SessionId, string CallbackUrl, DateTimeOffset ExpiresUtc);

/// <summary>GET /v1/poll response body.</summary>
public sealed record HostedPollResponse(
    string Status,
    string? Code,
    string? State,
    string? Error,
    string? ErrorDescription);
