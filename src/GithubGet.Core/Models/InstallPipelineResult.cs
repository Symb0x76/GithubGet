namespace GithubGet.Core.Models;

public sealed record InstallPipelineResult
{
    public required UpdateState State { get; init; }
    public string? DownloadedFilePath { get; init; }
    public string? ScriptPath { get; init; }
    public ScriptResult? ScriptResult { get; init; }
    public InstallResult? InstallResult { get; init; }
    public string? Message { get; init; }
}
