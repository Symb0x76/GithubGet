namespace GithubGet.Core.Models;

public sealed record ScriptExecutionContext
{
    public required string DownloadedFilePath { get; init; }
    public required string DownloadDirectory { get; init; }
    public required string ReleaseTag { get; init; }
    public string? ReleaseTitle { get; init; }
    public required string ReleaseUrl { get; init; }
    public required string SubscriptionId { get; init; }
    public required string SubscriptionName { get; init; }
}
