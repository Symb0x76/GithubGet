namespace GithubGet.Core.Services;

public sealed class GitHubApiRateLimitException : Exception
{
    public GitHubApiRateLimitException(
        string message,
        bool hasToken,
        DateTimeOffset? resetAtUtc = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        HasToken = hasToken;
        ResetAtUtc = resetAtUtc;
    }

    public bool HasToken { get; }
    public DateTimeOffset? ResetAtUtc { get; }
}
