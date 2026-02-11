namespace GithubGet.Core.Models;

public sealed record InstallRequest
{
    public required string FilePath { get; init; }
    public required InstallKind Kind { get; init; }
    public string? Args { get; init; }
    public bool RequireAdmin { get; init; }
    public int TimeoutSeconds { get; init; } = 1800;
    public bool AllowReboot { get; init; }
    public string? ExpectedPublisher { get; init; }
}
