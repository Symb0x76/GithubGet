using System.Net;
using System.Text;
using GithubGet.Core.Models;
using GithubGet.Core.Services;

namespace GithubGet.Core.Tests;

public class GitHubClientRateLimitTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_WhenRateLimited_ThrowsFriendlyException()
    {
        var resetAtUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        using var httpClient = new HttpClient(new RateLimitedHandler(resetAtUtc));
        var client = new GitHubClient(httpClient, new GitHubClientOptions());

        var exception = await Assert.ThrowsAsync<GitHubApiRateLimitException>(() =>
            client.GetLatestReleaseAsync(new RepositoryId("owner", "repo"), includePrerelease: false));

        Assert.False(exception.HasToken);
        Assert.Equal(resetAtUtc.ToUnixTimeSeconds(), exception.ResetAtUtc?.ToUnixTimeSeconds());
        Assert.Contains("rate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RateLimitedHandler : HttpMessageHandler
    {
        private readonly DateTimeOffset _resetAtUtc;

        public RateLimitedHandler(DateTimeOffset resetAtUtc)
        {
            _resetAtUtc = resetAtUtc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = """
            {
              "message": "API rate limit exceeded for 127.0.0.1."
            }
            """;

            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", _resetAtUtc.ToUnixTimeSeconds().ToString());
            return Task.FromResult(response);
        }
    }
}
