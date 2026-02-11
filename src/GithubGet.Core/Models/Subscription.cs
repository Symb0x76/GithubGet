namespace GithubGet.Core.Models;

public sealed record Subscription
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Owner { get; init; } = string.Empty;
    public string Repo { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public bool IncludePrerelease { get; init; }
    public AssetRuleSet AssetRules { get; init; } = AssetRuleSet.Default;
    public InstallKind InstallKind { get; init; } = InstallKind.Auto;
    public string? SilentArgs { get; init; }
    public string? MsiArgs { get; init; }
    public bool RequireAdmin { get; init; }
    public int TimeoutSeconds { get; init; } = 1800;
    public bool AllowReboot { get; init; }
    public string? ExpectedPublisher { get; init; }
    public bool PreInstallScriptEnabled { get; init; }
    public string? PreInstallScriptPath { get; init; }
    public string? PreInstallScriptArgs { get; init; }
    public bool PreInstallScriptRequireAdmin { get; init; }
    public long? LastSeenReleaseId { get; init; }
    public string? LastSeenTag { get; init; }
    public DateTimeOffset? LastCheckedAtUtc { get; init; }

    public string FullName => $"{Owner}/{Repo}";
    public string DisplayTitle => string.IsNullOrWhiteSpace(DisplayName) ? FullName : DisplayName;
}
