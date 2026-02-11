namespace GithubGet.Core.Services;

public sealed record GitHubClientOptions
{
    public string? Token { get; init; }
    public string UserAgent { get; init; } = "GithubGet/0.1";
}
