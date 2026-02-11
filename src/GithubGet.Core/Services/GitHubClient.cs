using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GithubGet.Core.Models;

namespace GithubGet.Core.Services;

public sealed class GitHubClient : IGitHubClient
{
    private readonly HttpClient _httpClient;
    private readonly IApiCache _cache;
    private readonly bool _hasToken;

    public GitHubClient(HttpClient httpClient, GitHubClientOptions options, IApiCache? cache = null)
    {
        _httpClient = httpClient;
        _cache = cache ?? new NullApiCache();
        _hasToken = !string.IsNullOrWhiteSpace(options.Token);

        _httpClient.BaseAddress ??= new Uri("https://api.github.com/");
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        }

        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }

        if (_hasToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }
    }

    public async Task<ReleaseInfo?> GetLatestReleaseAsync(RepositoryId repository, bool includePrerelease, CancellationToken ct = default)
    {
        if (includePrerelease)
        {
            var releases = await GetRecentReleasesAsync(repository, 10, ct);
            return releases.FirstOrDefault(r => !r.Draft && !r.Prerelease)
                ?? releases.FirstOrDefault(r => !r.Draft)
                ?? releases.FirstOrDefault();
        }

        var url = $"repos/{repository.Owner}/{repository.Repo}/releases/latest";
        var response = await SendAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        await EnsureSuccessAsync(response, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseRelease(json);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetRecentReleasesAsync(RepositoryId repository, int count, CancellationToken ct = default)
    {
        var url = $"repos/{repository.Owner}/{repository.Repo}/releases?per_page={count}";
        var response = await SendAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NotModified)
        {
            return Array.Empty<ReleaseInfo>();
        }

        await EnsureSuccessAsync(response, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseReleases(json);
    }

    public async Task<IReadOnlyList<RepositorySearchResult>> SearchRepositoriesAsync(string keyword, int count = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Array.Empty<RepositorySearchResult>();
        }

        var safeCount = Math.Clamp(count, 1, 50);
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var url = $"search/repositories?q={encodedKeyword}&sort=updated&order=desc&per_page={safeCount}";
        var response = await SendAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NotModified)
        {
            return Array.Empty<RepositorySearchResult>();
        }

        await EnsureSuccessAsync(response, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseRepositorySearchResults(json);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var etag = await _cache.GetEtagAsync(url, ct);
        if (!string.IsNullOrWhiteSpace(etag))
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        var response = await _httpClient.SendAsync(request, ct);
        if (response.Headers.ETag is not null)
        {
            await _cache.SetEtagAsync(url, response.Headers.ETag.ToString(), ct);
        }

        return response;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (IsRateLimited(response))
        {
            var message = await ReadRateLimitMessageAsync(response, ct);
            throw new GitHubApiRateLimitException(message, _hasToken, ReadRateLimitResetAtUtc(response));
        }

        response.EnsureSuccessStatusCode();
    }

    private static bool IsRateLimited(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return false;
        }

        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var values))
        {
            return false;
        }

        var remaining = values.FirstOrDefault();
        return long.TryParse(remaining, out var parsed) && parsed <= 0;
    }

    private static DateTimeOffset? ReadRateLimitResetAtUtc(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Reset", out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        if (!long.TryParse(raw, out var resetSeconds))
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(resetSeconds);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> ReadRateLimitMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("message", out var messageProp))
                {
                    var message = messageProp.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message;
                    }
                }
            }
        }
        catch
        {
        }

        return "GitHub API rate limit exceeded.";
    }

    private static ReleaseInfo ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseRelease(doc.RootElement);
    }

    private static IReadOnlyList<ReleaseInfo> ParseReleases(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ReleaseInfo>();
        }

        var list = new List<ReleaseInfo>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            list.Add(ParseRelease(element));
        }

        return list;
    }

    private static IReadOnlyList<RepositorySearchResult> ParseRepositorySearchResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RepositorySearchResult>();
        }

        var results = new List<RepositorySearchResult>();
        foreach (var item in items.EnumerateArray())
        {
            var fullName = item.TryGetProperty("full_name", out var fullNameProp)
                ? fullNameProp.GetString() ?? string.Empty
                : string.Empty;
            var repo = item.TryGetProperty("name", out var repoProp)
                ? repoProp.GetString() ?? string.Empty
                : string.Empty;
            var owner = string.Empty;
            if (item.TryGetProperty("owner", out var ownerProp) &&
                ownerProp.ValueKind == JsonValueKind.Object &&
                ownerProp.TryGetProperty("login", out var ownerLoginProp))
            {
                owner = ownerLoginProp.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(owner) && fullName.Contains('/'))
            {
                var parsed = RepositoryId.Parse(fullName);
                owner = parsed.Owner;
                if (string.IsNullOrWhiteSpace(repo))
                {
                    repo = parsed.Repo;
                }
            }

            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                continue;
            }

            var description = item.TryGetProperty("description", out var descriptionProp)
                ? descriptionProp.GetString()
                : null;
            var htmlUrl = item.TryGetProperty("html_url", out var htmlUrlProp)
                ? htmlUrlProp.GetString()
                : null;

            results.Add(new RepositorySearchResult
            {
                Owner = owner,
                Repo = repo,
                FullName = string.IsNullOrWhiteSpace(fullName) ? $"{owner}/{repo}" : fullName,
                Description = description,
                HtmlUrl = htmlUrl
            });
        }

        return results;
    }

    private static ReleaseInfo ParseRelease(JsonElement element)
    {
        var id = element.GetProperty("id").GetInt64();
        var tag = element.GetProperty("tag_name").GetString() ?? string.Empty;
        var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        var publishedAt = DateTimeOffset.MinValue;
        if (element.TryGetProperty("published_at", out var publishedProp) && publishedProp.ValueKind != JsonValueKind.Null)
        {
            var publishedText = publishedProp.GetString();
            if (!string.IsNullOrWhiteSpace(publishedText) && DateTimeOffset.TryParse(publishedText, out var parsed))
            {
                publishedAt = parsed;
            }
        }

        var prerelease = element.TryGetProperty("prerelease", out var prereleaseProp) && prereleaseProp.GetBoolean();
        var draft = element.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean();
        var htmlUrl = element.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? string.Empty : string.Empty;
        var body = element.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : null;
        var assets = new List<ReleaseAsset>();
        if (element.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsProp.EnumerateArray())
            {
                var assetName = asset.GetProperty("name").GetString() ?? string.Empty;
                var size = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                var downloadUrl = asset.TryGetProperty("browser_download_url", out var urlAssetProp)
                    ? urlAssetProp.GetString() ?? string.Empty
                    : string.Empty;
                var contentType = asset.TryGetProperty("content_type", out var typeProp) ? typeProp.GetString() : null;
                assets.Add(new ReleaseAsset(assetName, size, downloadUrl, contentType));
            }
        }

        return new ReleaseInfo(id, tag, name, publishedAt, prerelease, draft, htmlUrl, body, assets);
    }

    private sealed class NullApiCache : IApiCache
    {
        public Task<string?> GetEtagAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task SetEtagAsync(string key, string value, CancellationToken ct = default) => Task.CompletedTask;
    }
}
