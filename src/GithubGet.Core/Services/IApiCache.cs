namespace GithubGet.Core.Services;

public interface IApiCache
{
    Task<string?> GetEtagAsync(string key, CancellationToken ct = default);
    Task SetEtagAsync(string key, string value, CancellationToken ct = default);
}
