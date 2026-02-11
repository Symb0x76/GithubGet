namespace GithubGet.Core.Models;

public sealed record ScriptResult
{
    public required bool Succeeded { get; init; }
    public int? ExitCode { get; init; }
    public string? StandardOutput { get; init; }
    public string? StandardError { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset EndedAtUtc { get; init; }
    public string? Message { get; init; }
}
