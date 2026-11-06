using System.Net;
using System.Net.Http.Json;

namespace Liwa.IntegrationTests;

public sealed class RateLimitedApiFactory : LiwaApiFactory
{
    protected override int SignInPermitsPerMinute => 3;
}

public sealed class RateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Repeated_sign_in_attempts_are_throttled()
    {
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "guess" + i })).StatusCode);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized], statuses.Take(3));
        Assert.All(statuses.Skip(3), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }
}
