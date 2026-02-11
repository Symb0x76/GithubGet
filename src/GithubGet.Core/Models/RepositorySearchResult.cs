namespace GithubGet.Core.Models;

public sealed record RepositorySearchResult
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
    public string? HtmlUrl { get; init; }
}
