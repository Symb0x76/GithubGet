using GithubGet.Core.Models;
using GithubGet.Core.Services;
using GithubGet.Core.Storage;

namespace GithubGet.Core.Tests;

public class UpdateCheckerRateLimitTests
{
    [Fact]
    public async Task RunOnceAsync_WhenRateLimited_ReturnsRateLimitSummary()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var store = new SqliteGithubGetStore(dbPath);
            var subscription = new Subscription
            {
                Id = Guid.NewGuid().ToString("N"),
                Owner = "owner",
                Repo = "repo"
            };
            await store.UpsertSubscriptionAsync(subscription);

            var resetAtUtc = DateTimeOffset.UtcNow.AddMinutes(5);
            var client = new RateLimitClient(resetAtUtc);
            var checker = new UpdateChecker(store, client, new AssetSelector());

            var summary = await checker.RunOnceAsync(new UpdateCheckOptions
            {
                SubscriptionId = subscription.Id
            });

            Assert.Equal(1, summary.Checked);
            Assert.Equal(0, summary.Updated);
            Assert.Equal(1, summary.Failed);
            Assert.True(summary.RateLimited);
            Assert.False(summary.HasToken);
            Assert.Equal(resetAtUtc.ToUnixTimeSeconds(), summary.RateLimitResetAtUtc?.ToUnixTimeSeconds());
        }
        finally
        {
            DeleteTempDatabaseFile(dbPath);
        }
    }

    private static string CreateTempDatabasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "GithubGet.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "githubget.db");
    }

    private static void DeleteTempDatabaseFile(string dbPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
        }
    }

    private sealed class RateLimitClient : IGitHubClient
    {
        private readonly DateTimeOffset _resetAtUtc;

        public RateLimitClient(DateTimeOffset resetAtUtc)
        {
            _resetAtUtc = resetAtUtc;
        }

        public Task<ReleaseInfo?> GetLatestReleaseAsync(RepositoryId repository, bool includePrerelease, CancellationToken ct = default)
        {
            throw new GitHubApiRateLimitException("API rate limit exceeded.", hasToken: false, resetAtUtc: _resetAtUtc);
        }

        public Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(RepositoryId repository, int count, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
        }

        public Task<IReadOnlyList<RepositorySearchResult>> SearchRepositoriesAsync(string keyword, int count = 20, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<RepositorySearchResult>>(Array.Empty<RepositorySearchResult>());
        }
    }
}
