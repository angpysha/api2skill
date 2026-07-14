namespace Api2Skill.OAuth;

/// <summary>Inputs for a single redirect-capture attempt (CLI / login handoff).</summary>
public sealed record CaptureOptions(
    Uri CallbackUrl,
    CaptureMode? Mode = null,
    TimeSpan? Timeout = null,
    string? CertPath = null,
    string? CertPassword = null,
    string? CertPemPath = null,
    string? CertKeyPath = null,
    string? RelayBaseUrl = null,
    string? State = null,
    string? ExpectedPath = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(180);

    public string? EffectiveExpectedPath =>
        ExpectedPath ?? (string.IsNullOrEmpty(CallbackUrl.AbsolutePath) ? "/" : CallbackUrl.AbsolutePath);
}
