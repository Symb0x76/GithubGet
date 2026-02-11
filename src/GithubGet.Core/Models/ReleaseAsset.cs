namespace GithubGet.Core.Models;

public sealed record ReleaseAsset(
    string Name,
    long Size,
    string DownloadUrl,
    string? ContentType);
