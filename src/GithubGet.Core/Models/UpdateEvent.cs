namespace GithubGet.Core.Models;

public sealed record UpdateEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string SubscriptionId { get; init; } = string.Empty;
    public long ReleaseId { get; init; }
    public string Tag { get; init; } = string.Empty;
    public string? Title { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }
    public string HtmlUrl { get; init; } = string.Empty;
    public string? BodyMarkdown { get; init; }
    public SelectedAsset? SelectedAsset { get; init; }
    public string? DownloadedFilePath { get; init; }
    public string? ScriptPath { get; init; }
    public int? ScriptExitCode { get; init; }
    public string? ScriptStandardOutput { get; init; }
    public string? ScriptStandardError { get; init; }
    public int? InstallExitCode { get; init; }
    public string? InstallStandardOutput { get; init; }
    public string? InstallStandardError { get; init; }
    public string? ProcessingMessage { get; init; }
    public DateTimeOffset? ProcessedAtUtc { get; init; }
    public UpdateState State { get; init; } = UpdateState.New;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
