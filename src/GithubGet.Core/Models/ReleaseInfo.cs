namespace GithubGet.Core.Models;

public sealed record ReleaseInfo(
    long Id,
    string TagName,
    string? Name,
    DateTimeOffset PublishedAt,
    bool Prerelease,
    bool Draft,
    string HtmlUrl,
    string? Body,
    IReadOnlyList<ReleaseAsset> Assets);
