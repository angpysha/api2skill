namespace Api2Skill.OAuth;

/// <summary>How the app captures an OAuth redirect (data-model CaptureMode).</summary>
public enum CaptureMode
{
    HttpLoopback,
    HttpsLoopback,
    CustomScheme,
    Hosted,
}
