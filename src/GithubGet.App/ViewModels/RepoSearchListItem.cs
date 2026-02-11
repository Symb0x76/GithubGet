namespace GithubGet.App.ViewModels;

public sealed record RepoSearchListItem
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
    public string? LatestReleaseTag { get; init; }
    public string? Url { get; init; }
}
