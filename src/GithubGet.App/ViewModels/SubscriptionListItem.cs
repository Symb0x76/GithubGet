using GithubGet.Core.Models;

namespace GithubGet.App.ViewModels;

public sealed partial class SubscriptionListItem : ObservableObject
{
    private bool _isChecked;

    public SubscriptionListItem(
        Subscription subscription,
        string latestVersion,
        string currentVersion,
        string newVersion,
        string sourceText,
        DateTimeOffset? lastUpdatedAtUtc,
        string latestStateText,
        bool isChecked = false)
    {
        Subscription = subscription;
        LatestVersion = latestVersion;
        CurrentVersion = currentVersion;
        NewVersion = newVersion;
        SourceText = sourceText;
        LastUpdatedText = lastUpdatedAtUtc.HasValue
            ? lastUpdatedAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "未知";
        LatestStateText = latestStateText;
        _isChecked = isChecked;
    }

    public Subscription Subscription { get; }
    public string RepoName => Subscription.Repo;
    public string Owner => Subscription.Owner;
    public string LatestVersion { get; }
    public string CurrentVersion { get; }
    public string NewVersion { get; }
    public string SourceText { get; }
    public string PreReleaseText => Subscription.IncludePrerelease ? "是" : "否";
    public string LastUpdatedText { get; }
    public string LatestStateText { get; }
    public string FullName => Subscription.FullName;
    public string DisplayTitle => Subscription.DisplayTitle;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }
}
