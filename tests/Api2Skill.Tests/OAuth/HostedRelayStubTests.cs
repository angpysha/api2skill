using System.Net;
using System.Text;
using System.Text.Json;
using Api2Skill.OAuth;

namespace Api2Skill.Tests.OAuth;

/// <summary>T031 — relay contract stub tests matching <c>contracts/hosted-relay.md</c>.</summary>
public class HostedRelayStubTests
{
    [Fact]
    public async Task PostSession_Returns201_WithCallbackAndClampedTtl()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var client = new HttpClient { BaseAddress = server.BaseUri };

        var payload = """{"state":"oauth-state","ttlSeconds":999}""";
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("v1/session", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var session = JsonSerializer.Deserialize(json, HostedRelayJsonContext.Default.HostedSessionCreateResponse);
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Contains("sid=", session.CallbackUrl, StringComparison.Ordinal);
        Assert.True(session.ExpiresUtc <= DateTimeOffset.UtcNow.AddSeconds(300).AddSeconds(5));
    }

    [Fact]
    public async Task CallbackThenPoll_ReturnsCode_Then410OnSecondPoll()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var client = new HttpClient { BaseAddress = server.BaseUri };

        var created = await CreateSessionAsync(client, "st-1");
        var cb = await client.GetAsync($"v1/callback?sid={created.SessionId}&code=THE_CODE&state=st-1");
        Assert.Equal(HttpStatusCode.OK, cb.StatusCode);
        var html = await cb.Content.ReadAsStringAsync();
        Assert.Contains("close this window", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("THE_CODE", html, StringComparison.Ordinal);

        var poll1 = await client.GetAsync($"v1/poll?sid={created.SessionId}");
        Assert.Equal(HttpStatusCode.OK, poll1.StatusCode);
        var body1 = JsonSerializer.Deserialize(
            await poll1.Content.ReadAsStringAsync(),
            HostedRelayJsonContext.Default.HostedPollResponse);
        Assert.NotNull(body1);
        Assert.Equal("completed", body1.Status);
        Assert.Equal("THE_CODE", body1.Code);
        Assert.Equal("st-1", body1.State);
        Assert.Null(body1.Error);

        var poll2 = await client.GetAsync($"v1/poll?sid={created.SessionId}");
        Assert.Equal(HttpStatusCode.Gone, poll2.StatusCode);
    }

    [Fact]
    public async Task PollPending_ThenErrorCallback()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var client = new HttpClient { BaseAddress = server.BaseUri };

        var created = await CreateSessionAsync(client, "st-err");
        var pending = await client.GetAsync($"v1/poll?sid={created.SessionId}");
        var pendingBody = JsonSerializer.Deserialize(
            await pending.Content.ReadAsStringAsync(),
            HostedRelayJsonContext.Default.HostedPollResponse);
        Assert.Equal("pending", pendingBody?.Status);

        _ = await client.GetAsync(
            $"v1/callback?sid={created.SessionId}&error=access_denied&error_description=nope&state=st-err");

        var done = await client.GetAsync($"v1/poll?sid={created.SessionId}");
        var body = JsonSerializer.Deserialize(
            await done.Content.ReadAsStringAsync(),
            HostedRelayJsonContext.Default.HostedPollResponse);
        Assert.Equal("completed", body?.Status);
        Assert.Equal("access_denied", body?.Error);
        Assert.Equal("nope", body?.ErrorDescription);
        Assert.Null(body?.Code);
    }

    [Fact]
    public async Task UnknownSid_PollReturns410()
    {
        await using var server = await TestHostedRelayServer.StartAsync();
        using var client = new HttpClient { BaseAddress = server.BaseUri };
        var response = await client.GetAsync("v1/poll?sid=does-not-exist");
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    private static async Task<HostedSessionCreateResponse> CreateSessionAsync(HttpClient client, string state)
    {
        var payload = JsonSerializer.Serialize(
            new HostedSessionCreateRequest(state, 300),
            HostedRelayJsonContext.Default.HostedSessionCreateRequest);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("v1/session", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize(json, HostedRelayJsonContext.Default.HostedSessionCreateResponse)!;
    }
}
