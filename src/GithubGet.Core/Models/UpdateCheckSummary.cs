namespace GithubGet.Core.Models;

public sealed record UpdateCheckSummary(
    int Checked,
    int Updated,
    int Failed,
    bool RateLimited = false,
    bool HasToken = false,
    DateTimeOffset? RateLimitResetAtUtc = null);
