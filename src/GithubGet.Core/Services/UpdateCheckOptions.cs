namespace GithubGet.Core.Services;

public sealed record UpdateCheckOptions
{
    public string? SubscriptionId { get; init; }
    public bool NoToast { get; init; }
    public bool InstallAssets { get; init; }
}
