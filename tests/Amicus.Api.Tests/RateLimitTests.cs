using System.Net;
using System.Net.Http.Json;

namespace Amicus.Api.Tests;

/// <summary>
/// Its own factory, with a deliberately tiny auth limit. The shared fixture runs
/// at effectively unlimited rates so the rest of the suite is not throttled, which
/// means the limiter would otherwise never be exercised at all.
/// </summary>
/// <remarks>
/// In the shared collection despite having its own factory, purely to be
/// SERIALISED against the other tests. Outside it, xunit runs this class in
/// parallel with them and its ResetAsync truncates the database they are midway
/// through using — which surfaced as /auth/manage/info returning 404, because
/// Identity answers 404 when a valid token's user has vanished.
/// </remarks>
[Collection(AmicusCollection.Name)]
public sealed class RateLimitTests : IAsyncLifetime
{
    private readonly AmicusAppFactory _app = new();

    public async Task InitializeAsync()
    {
        _app.Overrides["RateLimits:AuthPermitsPerMinute"] = "3";
        await _app.EnsureDatabaseAsync();
        await _app.ResetAsync();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Auth_endpoints_start_refusing_after_the_limit()
    {
        var client = _app.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/auth/login", new { email = $"nobody{i}@amicus.test", password = "wrong-wrong" });
            statuses.Add(response.StatusCode);
        }

        // The first three are allowed through (and fail on credentials, which is
        // fine — the point is that they reached the endpoint). The rest are shed.
        Assert.Equal(3, statuses.Count(s => s != HttpStatusCode.TooManyRequests));
        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task A_throttled_response_says_when_to_retry()
    {
        var client = _app.CreateClient();
        HttpResponseMessage? throttled = null;

        for (var i = 0; i < 8 && throttled is null; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/auth/login", new { email = "nobody@amicus.test", password = "wrong-wrong" });

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttled = response;
            }
        }

        Assert.NotNull(throttled);
        Assert.NotNull(throttled!.Headers.RetryAfter);
    }

    [Fact]
    public async Task Health_is_never_throttled()
    {
        var client = _app.CreateClient();

        // Well past the auth limit; an uptime monitor must not be shed.
        for (var i = 0; i < 30; i++)
        {
            var response = await client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
