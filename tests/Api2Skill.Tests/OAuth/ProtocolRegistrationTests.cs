using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

public class ProtocolRegistrationTests
{
    [Fact]
    public void DefaultScheme_IsApi2Skill()
    {
        Assert.Equal("api2skill", ProtocolRegistration.DefaultScheme);
    }

    [Theory]
    [InlineData("api2skill", true)]
    [InlineData("API2SKILL", true)]
    [InlineData("myapp", false)]
    [InlineData("http", false)]
    public void IsFirstPartyScheme_MatchesDefault(string scheme, bool expected)
    {
        Assert.Equal(expected, ProtocolRegistration.IsFirstPartyScheme(scheme));
    }

    [Fact]
    public void ResolveHandlerCommand_IncludesExecutable()
    {
        var command = ProtocolRegistration.ResolveHandlerCommand();
        Assert.False(string.IsNullOrWhiteSpace(command));
        Assert.Contains(Environment.ProcessPath ?? "dotnet", command, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Register_Unregister_RoundTrip_OnCurrentOs()
    {
        if (!ProtocolRegistration.IsSupportedPlatform)
        {
            return;
        }

        var scheme = $"a2stest{Guid.NewGuid():N}"[..16];
        try
        {
            Assert.False(ProtocolRegistration.IsRegistered(scheme));
            ProtocolRegistration.Register(scheme, force: true);
            Assert.True(ProtocolRegistration.IsRegistered(scheme));
            ProtocolRegistration.Unregister(scheme);
            Assert.False(ProtocolRegistration.IsRegistered(scheme));
        }
        finally
        {
            try { ProtocolRegistration.Unregister(scheme); } catch { }
        }
    }

    [Fact]
    public void Register_WithoutForce_Throws_WhenAlreadyRegistered()
    {
        if (!ProtocolRegistration.IsSupportedPlatform)
        {
            return;
        }

        var scheme = $"a2sfrc{Guid.NewGuid():N}"[..16];
        try
        {
            ProtocolRegistration.Register(scheme, force: true);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProtocolRegistration.Register(scheme, force: false));
            Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { ProtocolRegistration.Unregister(scheme); } catch { }
        }
    }

    [Fact]
    public void UnsupportedPlatform_ThrowsClearMessage()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            return;
        }

        var ex = Assert.Throws<PlatformNotSupportedException>(() =>
            ProtocolRegistration.Register("api2skill", force: true));
        Assert.Contains("register-protocol", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
