using GithubGet.Core.Models;

namespace GithubGet.Core.Services;

public interface IGitHubClient
{
    Task<ReleaseInfo?> GetLatestReleaseAsync(RepositoryId repository, bool includePrerelease, CancellationToken ct = default);
    Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(RepositoryId repository, int count, CancellationToken ct = default);
    Task<IReadOnlyList<RepositorySearchResult>> SearchRepositoriesAsync(string keyword, int count = 20, CancellationToken ct = default);
}
