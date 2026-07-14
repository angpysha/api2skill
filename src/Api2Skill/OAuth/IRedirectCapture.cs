namespace Api2Skill.OAuth;

/// <summary>App-owned OAuth redirect capture (HTTP / HTTPS / scheme / hosted).</summary>
public interface IRedirectCapture
{
    Task<CaptureResult> CaptureAsync(CaptureOptions options, CancellationToken cancellationToken = default);
}
