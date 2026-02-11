namespace GithubGet.Core.Models;

public sealed record ScriptExecutionRequest
{
    public required string ScriptPath { get; init; }
    public string? Args { get; init; }
    public bool RequireAdmin { get; init; }
    public int TimeoutSeconds { get; init; } = 1800;
    public required ScriptExecutionContext Context { get; init; }
}
