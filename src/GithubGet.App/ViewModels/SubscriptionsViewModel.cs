using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using GithubGet.App.Services;
using GithubGet.Core.Models;
using GithubGet.Core.Services;
using GithubGet.Core.Storage;
using Microsoft.UI.Xaml;

namespace GithubGet.App.ViewModels;

public partial class SubscriptionsViewModel : BaseViewModel
{
    private enum InstalledSearchMode
    {
        Owner,
        Repo,
        OwnerOrRepo
    }

    private enum InstalledViewMode
    {
        Detailed,
        Compact,
        Card
    }

    private enum UpdateSearchMode
    {
        Owner,
        Repo,
        OwnerOrRepo
    }

    private enum UpdateViewMode
    {
        Detailed,
        Compact,
        Card
    }

    private const string IgnoredUpdatesSettingKey = "updates.ignored.releases";
    private readonly IGithubGetStore _store = AppServices.Store;
    private static readonly char[] InputSeparators = { ',', ';', '\r', '\n' };
    private static readonly JsonSerializerOptions BackupJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private bool _initialized;
    private bool _isLoading;
    private string _ownerInput = string.Empty;
    private string _repoInput = string.Empty;
    private string _displayNameInput = string.Empty;
    private bool _includePrereleaseInput;
    private bool _isRepoSearchRunning;
    private bool _hasRepoSearchRequested;
    private string _repoSearchInput = string.Empty;
    private ObservableCollection<RepoSearchListItem> _repoSearchResults = new();
    private string _statusMessage = string.Empty;
    private ObservableCollection<Subscription> _subscriptions = new();
    private ObservableCollection<SubscriptionListItem> _visibleSubscriptions = new();
    private ObservableCollection<SubscriptionListItem> _visibleUpdateSubscriptions = new();
    private Dictionary<string, DateTimeOffset?> _latestReleasePublishedAtBySubscriptionId = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _latestUpdateStateBySubscriptionId = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _installedVersionBySubscriptionId = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, long> _ignoredReleaseBySubscriptionId = new(StringComparer.OrdinalIgnoreCase);
    private string _subscriptionSearchInput = string.Empty;
    private string _subscriptionSortInput = "名称";
    private bool _isRealtimeSearchInput = true;
    private bool _isCaseSensitiveSearchInput;
    private bool _isIgnoreSpecialCharsSearchInput = true;
    private InstalledSearchMode _installedSearchMode = InstalledSearchMode.OwnerOrRepo;
    private InstalledViewMode _installedViewMode = InstalledViewMode.Detailed;
    private string _updateSearchInput = string.Empty;
    private string _updateSortInput = "名称";
    private bool _isUpdateRealtimeSearchInput = true;
    private bool _isUpdateCaseSensitiveSearchInput;
    private bool _isUpdateIgnoreSpecialCharsSearchInput = true;
    private UpdateSearchMode _updateSearchMode = UpdateSearchMode.OwnerOrRepo;
    private UpdateViewMode _updateViewMode = UpdateViewMode.Detailed;
    private Subscription? _selectedSubscription;
    private string _editDisplayNameInput = string.Empty;
    private bool _editIncludePrereleaseInput;
    private string _includeRulesInput = string.Empty;
    private string _excludeRulesInput = string.Empty;
    private string _preferExtensionsInput = string.Empty;
    private string _preferArchInput = string.Empty;
    private string _preferKeywordsInput = string.Empty;
    private string _installKindInput = InstallKind.Auto.ToString();
    private string _silentArgsInput = string.Empty;
    private string _msiArgsInput = string.Empty;
    private bool _requireAdminInput;
    private string _timeoutSecondsInput = "1800";
    private bool _allowRebootInput;
    private string _expectedPublisherInput = string.Empty;
    private bool _preInstallScriptEnabledInput;
    private string _preInstallScriptPathInput = string.Empty;
    private string _preInstallScriptArgsInput = string.Empty;
    private bool _preInstallScriptRequireAdminInput;
    private string _configFilePathInput = GetDefaultConfigFilePath();

    public SubscriptionsViewModel()
    {
        Title = "已安装软件";
    }

    public IReadOnlyList<string> SubscriptionSortOptions { get; } = new[]
    {
        "名称",
        "仓库",
        "最近检查"
    };

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string OwnerInput
    {
        get => _ownerInput;
        set => SetProperty(ref _ownerInput, value);
    }

    public string RepoInput
    {
        get => _repoInput;
        set => SetProperty(ref _repoInput, value);
    }

    public string DisplayNameInput
    {
        get => _displayNameInput;
        set => SetProperty(ref _displayNameInput, value);
    }

    public bool IncludePrereleaseInput
    {
        get => _includePrereleaseInput;
        set => SetProperty(ref _includePrereleaseInput, value);
    }

    public bool IsRepoSearchRunning
    {
        get => _isRepoSearchRunning;
        set
        {
            if (SetProperty(ref _isRepoSearchRunning, value))
            {
                OnPropertyChanged(nameof(RepoSearchEmptyStateVisibility));
            }
        }
    }

    public string RepoSearchInput
    {
        get => _repoSearchInput;
        set => SetProperty(ref _repoSearchInput, value);
    }

    public ObservableCollection<RepoSearchListItem> RepoSearchResults
    {
        get => _repoSearchResults;
        set
        {
            if (SetProperty(ref _repoSearchResults, value))
            {
                OnPropertyChanged(nameof(RepoSearchEmptyStateVisibility));
            }
        }
    }

    public Visibility RepoSearchEmptyStateVisibility =>
        _hasRepoSearchRequested && !IsRepoSearchRunning && RepoSearchResults.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<Subscription> Subscriptions
    {
        get => _subscriptions;
        set
        {
            if (SetProperty(ref _subscriptions, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public ObservableCollection<SubscriptionListItem> VisibleSubscriptions
    {
        get => _visibleSubscriptions;
        set => SetProperty(ref _visibleSubscriptions, value);
    }

    public ObservableCollection<SubscriptionListItem> VisibleUpdateSubscriptions
    {
        get => _visibleUpdateSubscriptions;
        set
        {
            if (SetProperty(ref _visibleUpdateSubscriptions, value))
            {
                OnPropertyChanged(nameof(UpdatesEmptyStateVisibility));
                OnPropertyChanged(nameof(UpdateSourceStatusText));
            }
        }
    }

    public string SubscriptionSearchInput
    {
        get => _subscriptionSearchInput;
        set
        {
            if (SetProperty(ref _subscriptionSearchInput, value))
            {
                if (IsRealtimeSearchInput)
                {
                    ApplySubscriptionView();
                }
            }
        }
    }

    public string SubscriptionSortInput
    {
        get => _subscriptionSortInput;
        set
        {
            if (SetProperty(ref _subscriptionSortInput, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool IsRealtimeSearchInput
    {
        get => _isRealtimeSearchInput;
        set
        {
            if (!SetProperty(ref _isRealtimeSearchInput, value))
            {
                return;
            }

            if (value)
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool IsCaseSensitiveSearchInput
    {
        get => _isCaseSensitiveSearchInput;
        set
        {
            if (SetProperty(ref _isCaseSensitiveSearchInput, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool IsIgnoreSpecialCharsSearchInput
    {
        get => _isIgnoreSpecialCharsSearchInput;
        set
        {
            if (SetProperty(ref _isIgnoreSpecialCharsSearchInput, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool InstalledSearchModeOwnerInput
    {
        get => _installedSearchMode == InstalledSearchMode.Owner;
        set
        {
            if (value)
            {
                SetInstalledSearchMode(InstalledSearchMode.Owner);
            }
        }
    }

    public bool InstalledSearchModeRepoInput
    {
        get => _installedSearchMode == InstalledSearchMode.Repo;
        set
        {
            if (value)
            {
                SetInstalledSearchMode(InstalledSearchMode.Repo);
            }
        }
    }

    public bool InstalledSearchModeOwnerOrRepoInput
    {
        get => _installedSearchMode == InstalledSearchMode.OwnerOrRepo;
        set
        {
            if (value)
            {
                SetInstalledSearchMode(InstalledSearchMode.OwnerOrRepo);
            }
        }
    }

    public bool InstalledViewModeDetailedInput
    {
        get => _installedViewMode == InstalledViewMode.Detailed;
        set
        {
            if (value)
            {
                SetInstalledViewMode(InstalledViewMode.Detailed);
            }
        }
    }

    public bool InstalledViewModeCompactInput
    {
        get => _installedViewMode == InstalledViewMode.Compact;
        set
        {
            if (value)
            {
                SetInstalledViewMode(InstalledViewMode.Compact);
            }
        }
    }

    public bool InstalledViewModeCardInput
    {
        get => _installedViewMode == InstalledViewMode.Card;
        set
        {
            if (value)
            {
                SetInstalledViewMode(InstalledViewMode.Card);
            }
        }
    }

    public Visibility InstalledTableHeaderVisibility =>
        _installedViewMode == InstalledViewMode.Card ? Visibility.Collapsed : Visibility.Visible;

    public Visibility InstalledDetailedViewVisibility =>
        _installedViewMode == InstalledViewMode.Detailed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InstalledCompactViewVisibility =>
        _installedViewMode == InstalledViewMode.Compact ? Visibility.Visible : Visibility.Collapsed;

    public Visibility InstalledCardViewVisibility =>
        _installedViewMode == InstalledViewMode.Card ? Visibility.Visible : Visibility.Collapsed;

    public string UpdateSearchInput
    {
        get => _updateSearchInput;
        set
        {
            if (SetProperty(ref _updateSearchInput, value))
            {
                if (IsUpdateRealtimeSearchInput)
                {
                    ApplySubscriptionView();
                }
            }
        }
    }

    public string UpdateSortInput
    {
        get => _updateSortInput;
        set
        {
            if (SetProperty(ref _updateSortInput, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool IsUpdateRealtimeSearchInput
    {
        get => _isUpdateRealtimeSearchInput;
        set
        {
            if (!SetProperty(ref _isUpdateRealtimeSearchInput, value))
            {
                return;
            }

            if (value)
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool IsUpdateCaseSensitiveSearchInput
    {
        get => _isUpdateCaseSensitiveSearchInput;
        set
        {
            if (SetProperty(ref _isUpdateCaseSensitiveSearchInput, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool IsUpdateIgnoreSpecialCharsSearchInput
    {
        get => _isUpdateIgnoreSpecialCharsSearchInput;
        set
        {
            if (SetProperty(ref _isUpdateIgnoreSpecialCharsSearchInput, value))
            {
                ApplySubscriptionView();
            }
        }
    }

    public bool UpdateSearchModeOwnerInput
    {
        get => _updateSearchMode == UpdateSearchMode.Owner;
        set
        {
            if (value)
            {
                SetUpdateSearchMode(UpdateSearchMode.Owner);
            }
        }
    }

    public bool UpdateSearchModeRepoInput
    {
        get => _updateSearchMode == UpdateSearchMode.Repo;
        set
        {
            if (value)
            {
                SetUpdateSearchMode(UpdateSearchMode.Repo);
            }
        }
    }

    public bool UpdateSearchModeOwnerOrRepoInput
    {
        get => _updateSearchMode == UpdateSearchMode.OwnerOrRepo;
        set
        {
            if (value)
            {
                SetUpdateSearchMode(UpdateSearchMode.OwnerOrRepo);
            }
        }
    }

    public bool UpdateViewModeDetailedInput
    {
        get => _updateViewMode == UpdateViewMode.Detailed;
        set
        {
            if (value)
            {
                SetUpdateViewMode(UpdateViewMode.Detailed);
            }
        }
    }

    public bool UpdateViewModeCompactInput
    {
        get => _updateViewMode == UpdateViewMode.Compact;
        set
        {
            if (value)
            {
                SetUpdateViewMode(UpdateViewMode.Compact);
            }
        }
    }

    public bool UpdateViewModeCardInput
    {
        get => _updateViewMode == UpdateViewMode.Card;
        set
        {
            if (value)
            {
                SetUpdateViewMode(UpdateViewMode.Card);
            }
        }
    }

    public Visibility UpdateTableHeaderVisibility =>
        _updateViewMode == UpdateViewMode.Card ? Visibility.Collapsed : Visibility.Visible;

    public Visibility UpdateDetailedViewVisibility =>
        _updateViewMode == UpdateViewMode.Detailed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UpdateCompactViewVisibility =>
        _updateViewMode == UpdateViewMode.Compact ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UpdateCardViewVisibility =>
        _updateViewMode == UpdateViewMode.Card ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UpdatesEmptyStateVisibility =>
        VisibleUpdateSubscriptions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string UpdateSourceStatusText =>
        VisibleUpdateSubscriptions.Count == 0
            ? "所有软件包均已为最新版本"
            : $"共 {VisibleUpdateSubscriptions.Count} 个软件包待更新";

    public Subscription? SelectedSubscription
    {
        get => _selectedSubscription;
        set
        {
            if (!SetProperty(ref _selectedSubscription, value))
            {
                return;
            }

            LoadEditorFromSubscription(value);
            OnPropertyChanged(nameof(IsEditorEnabled));
            OnPropertyChanged(nameof(SelectedSubscriptionTitle));
            OnPropertyChanged(nameof(ProjectScriptPathHint));
        }
    }

    public bool IsEditorEnabled => SelectedSubscription is not null;

    public string SelectedSubscriptionTitle => SelectedSubscription?.FullName ?? "未选择软件包";

    public string ProjectScriptPathHint => SelectedSubscription is null
        ? "请选择软件包后查看项目脚本路径。"
        : StorePaths.GetProjectScriptPath(SelectedSubscription.Owner, SelectedSubscription.Repo);

    public string EditDisplayNameInput
    {
        get => _editDisplayNameInput;
        set => SetProperty(ref _editDisplayNameInput, value);
    }

    public bool EditIncludePrereleaseInput
    {
        get => _editIncludePrereleaseInput;
        set => SetProperty(ref _editIncludePrereleaseInput, value);
    }

    public string IncludeRulesInput
    {
        get => _includeRulesInput;
        set => SetProperty(ref _includeRulesInput, value);
    }

    public string ExcludeRulesInput
    {
        get => _excludeRulesInput;
        set => SetProperty(ref _excludeRulesInput, value);
    }

    public string PreferExtensionsInput
    {
        get => _preferExtensionsInput;
        set => SetProperty(ref _preferExtensionsInput, value);
    }

    public string PreferArchInput
    {
        get => _preferArchInput;
        set => SetProperty(ref _preferArchInput, value);
    }

    public string PreferKeywordsInput
    {
        get => _preferKeywordsInput;
        set => SetProperty(ref _preferKeywordsInput, value);
    }

    public string InstallKindInput
    {
        get => _installKindInput;
        set => SetProperty(ref _installKindInput, value);
    }

    public string SilentArgsInput
    {
        get => _silentArgsInput;
        set => SetProperty(ref _silentArgsInput, value);
    }

    public string MsiArgsInput
    {
        get => _msiArgsInput;
        set => SetProperty(ref _msiArgsInput, value);
    }

    public bool RequireAdminInput
    {
        get => _requireAdminInput;
        set => SetProperty(ref _requireAdminInput, value);
    }

    public string TimeoutSecondsInput
    {
        get => _timeoutSecondsInput;
        set => SetProperty(ref _timeoutSecondsInput, value);
    }

    public bool AllowRebootInput
    {
        get => _allowRebootInput;
        set => SetProperty(ref _allowRebootInput, value);
    }

    public string ExpectedPublisherInput
    {
        get => _expectedPublisherInput;
        set => SetProperty(ref _expectedPublisherInput, value);
    }

    public bool PreInstallScriptEnabledInput
    {
        get => _preInstallScriptEnabledInput;
        set => SetProperty(ref _preInstallScriptEnabledInput, value);
    }

    public string PreInstallScriptPathInput
    {
        get => _preInstallScriptPathInput;
        set => SetProperty(ref _preInstallScriptPathInput, value);
    }

    public string PreInstallScriptArgsInput
    {
        get => _preInstallScriptArgsInput;
        set => SetProperty(ref _preInstallScriptArgsInput, value);
    }

    public bool PreInstallScriptRequireAdminInput
    {
        get => _preInstallScriptRequireAdminInput;
        set => SetProperty(ref _preInstallScriptRequireAdminInput, value);
    }

    public string ConfigFilePathInput
    {
        get => _configFilePathInput;
        set => SetProperty(ref _configFilePathInput, value);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private void ApplyInstalledFilter()
    {
        ApplySubscriptionView();
    }

    [RelayCommand]
    private void ApplyUpdatesFilter()
    {
        ApplySubscriptionView();
    }

    [RelayCommand]
    private async Task IgnoreSelectedUpdatesAsync()
    {
        var selectedSubscriptions = GetCheckedSubscriptions();
        if (selectedSubscriptions.Count == 0)
        {
            StatusMessage = "请先勾选要忽略更新的软件包。";
            return;
        }

        var ignoredCount = 0;
        var skippedCount = 0;
        foreach (var subscription in selectedSubscriptions)
        {
            if (!subscription.LastSeenReleaseId.HasValue)
            {
                skippedCount++;
                continue;
            }

            _ignoredReleaseBySubscriptionId[subscription.Id] = subscription.LastSeenReleaseId.Value;
            ignoredCount++;
        }

        await SaveIgnoredUpdatesAsync();
        foreach (var item in VisibleSubscriptions)
        {
            item.IsChecked = false;
        }

        foreach (var item in VisibleUpdateSubscriptions)
        {
            item.IsChecked = false;
        }

        ApplySubscriptionView();
        StatusMessage = $"已忽略 {ignoredCount} 个软件包的当前更新，跳过 {skippedCount} 个。";
    }

    [RelayCommand]
    private async Task ManageIgnoredUpdatesAsync()
    {
        if (_ignoredReleaseBySubscriptionId.Count == 0)
        {
            StatusMessage = "当前没有已忽略更新。";
            return;
        }

        var selectedSubscriptions = GetCheckedSubscriptions();
        if (selectedSubscriptions.Count > 0)
        {
            var removedCount = 0;
            foreach (var subscription in selectedSubscriptions)
            {
                if (_ignoredReleaseBySubscriptionId.Remove(subscription.Id))
                {
                    removedCount++;
                }
            }

            await SaveIgnoredUpdatesAsync();
            ApplySubscriptionView();
            StatusMessage = removedCount == 0
                ? "所选软件包中没有已忽略更新。"
                : $"已恢复 {removedCount} 个软件包的更新显示。";
            return;
        }

        var clearedCount = _ignoredReleaseBySubscriptionId.Count;
        _ignoredReleaseBySubscriptionId.Clear();
        await SaveIgnoredUpdatesAsync();
        ApplySubscriptionView();
        StatusMessage = $"已清空 {clearedCount} 条忽略更新记录。";
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var selectedId = SelectedSubscription?.Id;
        IsLoading = true;
        try
        {
            await _store.InitializeAsync();
            await LoadIgnoredUpdatesAsync();
            var items = await _store.GetSubscriptionsAsync();
            _installedVersionBySubscriptionId = InstalledAppVersionDetector.ResolveInstalledVersions(items);
            var validSubscriptionIds = items
                .Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _ignoredReleaseBySubscriptionId = _ignoredReleaseBySubscriptionId
                .Where(item => validSubscriptionIds.Contains(item.Key))
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            await SaveIgnoredUpdatesAsync();
            var updateEvents = await _store.GetUpdateEventsAsync(limit: 1000);
            _latestReleasePublishedAtBySubscriptionId = updateEvents
                .GroupBy(item => item.SubscriptionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (DateTimeOffset?)group
                        .OrderByDescending(item => item.PublishedAtUtc)
                        .First()
                        .PublishedAtUtc,
                    StringComparer.OrdinalIgnoreCase);
            _latestUpdateStateBySubscriptionId = updateEvents
                .GroupBy(item => item.SubscriptionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => ToUpdateStateText(group.OrderByDescending(item => item.CreatedAtUtc).First().State),
                    StringComparer.OrdinalIgnoreCase);

            Subscriptions = new ObservableCollection<Subscription>(items.OrderBy(s => s.DisplayTitle));
            SelectedSubscription = string.IsNullOrWhiteSpace(selectedId)
                ? null
                : Subscriptions.FirstOrDefault(s => string.Equals(s.Id, selectedId, StringComparison.OrdinalIgnoreCase));
            StatusMessage = $"已加载 {Subscriptions.Count} 个软件包";
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取软件包失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchReposAsync()
    {
        var keyword = RepoSearchInput.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            StatusMessage = "请输入仓库搜索关键字。";
            return;
        }

        _hasRepoSearchRequested = true;
        OnPropertyChanged(nameof(RepoSearchEmptyStateVisibility));
        IsRepoSearchRunning = true;
        var token = await AppServices.GetGitHubTokenAsync();
        var hasToken = !string.IsNullOrWhiteSpace(token);
        try
        {
            var client = AppServices.CreateGitHubClient(token);
            var repositories = await client.SearchRepositoriesAsync(keyword, 15);

            if (repositories.Count == 0)
            {
                RepoSearchResults = new ObservableCollection<RepoSearchListItem>();
                StatusMessage = $"未找到匹配仓库。{BuildPatHint(hasToken)}";
                return;
            }

            using var semaphore = new SemaphoreSlim(4);
            var rateLimitLock = new object();
            GitHubApiRateLimitException? rateLimitException = null;
            var tasks = repositories.Select(async repository =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var latestRelease = await client.GetLatestReleaseAsync(
                        new RepositoryId(repository.Owner, repository.Repo),
                        includePrerelease: true);
                    if (latestRelease is null)
                    {
                        return null;
                    }

                    return new RepoSearchListItem
                    {
                        Owner = repository.Owner,
                        Repo = repository.Repo,
                        FullName = repository.FullName,
                        Description = repository.Description,
                        LatestReleaseTag = latestRelease.TagName,
                        Url = repository.HtmlUrl
                    };
                }
                catch (GitHubApiRateLimitException ex)
                {
                    lock (rateLimitLock)
                    {
                        rateLimitException ??= ex;
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var filtered = (await Task.WhenAll(tasks))
                .Where(item => item is not null)
                .Select(item => item!)
                .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rateLimitException is not null)
            {
                RepoSearchResults = new ObservableCollection<RepoSearchListItem>();
                StatusMessage = BuildRateLimitStatusMessage(rateLimitException.HasToken, rateLimitException.ResetAtUtc);
                return;
            }

            RepoSearchResults = new ObservableCollection<RepoSearchListItem>(filtered);
            StatusMessage = filtered.Count == 0
                ? "未找到可添加软件包的仓库（已过滤无 Release 的仓库）。"
                : $"找到 {filtered.Count} 个可添加软件包的仓库（已过滤无 Release）。";
            StatusMessage = $"{StatusMessage}{BuildPatHint(hasToken)}";
        }
        catch (GitHubApiRateLimitException ex)
        {
            RepoSearchResults = new ObservableCollection<RepoSearchListItem>();
            StatusMessage = BuildRateLimitStatusMessage(ex.HasToken, ex.ResetAtUtc);
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索仓库失败: {ex.Message}";
        }
        finally
        {
            IsRepoSearchRunning = false;
        }
    }

    [RelayCommand]
    private void ApplyRepoFromSearch(RepoSearchListItem? item)
    {
        if (item is null)
        {
            return;
        }

        OwnerInput = item.Owner;
        RepoInput = item.Repo;
        if (string.IsNullOrWhiteSpace(DisplayNameInput))
        {
            DisplayNameInput = item.FullName;
        }

        StatusMessage = $"已载入 {item.FullName}，可直接添加软件包。";
    }

    [RelayCommand]
    private void OpenRepoPage(RepoSearchListItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Url))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.Url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开仓库页面失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddSubscriptionAsync()
    {
        var owner = OwnerInput.Trim();
        var repo = RepoInput.Trim();
        if (string.IsNullOrWhiteSpace(owner) && repo.Contains('/'))
        {
            var repositoryId = RepositoryId.Parse(repo);
            owner = repositoryId.Owner;
            repo = repositoryId.Repo;
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            StatusMessage = "请输入 owner 和 repo（或直接输入 owner/repo）。";
            return;
        }

        var exists = Subscriptions.Any(s =>
            string.Equals(s.Owner, owner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Repo, repo, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            StatusMessage = $"软件包 {owner}/{repo} 已存在。";
            return;
        }

        var subscription = new Subscription
        {
            Owner = owner,
            Repo = repo,
            DisplayName = string.IsNullOrWhiteSpace(DisplayNameInput) ? null : DisplayNameInput.Trim(),
            IncludePrerelease = IncludePrereleaseInput
        };

        IsLoading = true;
        try
        {
            await _store.UpsertSubscriptionAsync(subscription);
            Subscriptions.Add(subscription);
            Subscriptions = new ObservableCollection<Subscription>(Subscriptions.OrderBy(s => s.DisplayTitle));
            SelectedSubscription = Subscriptions.FirstOrDefault(s => string.Equals(s.Id, subscription.Id, StringComparison.OrdinalIgnoreCase));

            OwnerInput = string.Empty;
            RepoInput = string.Empty;
            DisplayNameInput = string.Empty;
            IncludePrereleaseInput = false;

            var token = await AppServices.GetGitHubTokenAsync();
            var hasToken = !string.IsNullOrWhiteSpace(token);
            var checkedAtUtc = DateTimeOffset.UtcNow;
            var client = AppServices.CreateGitHubClient(token);
            var release = await client.GetLatestReleaseAsync(new RepositoryId(subscription.Owner, subscription.Repo), subscription.IncludePrerelease);

            if (release is null)
            {
                var noReleaseSubscription = subscription with { LastCheckedAtUtc = checkedAtUtc };
                await _store.UpsertSubscriptionAsync(noReleaseSubscription);
                _latestUpdateStateBySubscriptionId[noReleaseSubscription.Id] = "未发现更新";
                ReplaceSubscriptionInView(noReleaseSubscription);
                SelectedSubscription = Subscriptions.FirstOrDefault(s => string.Equals(s.Id, noReleaseSubscription.Id, StringComparison.OrdinalIgnoreCase));
                StatusMessage = $"已添加 {subscription.FullName}，当前未发现可用 Release。{BuildPatHint(hasToken)}";
                return;
            }

            var updatedSubscription = subscription with
            {
                LastSeenReleaseId = release.Id,
                LastSeenTag = release.TagName,
                LastCheckedAtUtc = checkedAtUtc
            };
            await _store.UpsertSubscriptionAsync(updatedSubscription);
            _latestReleasePublishedAtBySubscriptionId[updatedSubscription.Id] = release.PublishedAt;

            var selectedAsset = new AssetSelector().Select(release, updatedSubscription.AssetRules);
            await _store.AddUpdateEventAsync(new UpdateEvent
            {
                SubscriptionId = updatedSubscription.Id,
                ReleaseId = release.Id,
                Tag = release.TagName,
                Title = release.Name,
                PublishedAtUtc = release.PublishedAt,
                HtmlUrl = release.HtmlUrl,
                BodyMarkdown = release.Body,
                SelectedAsset = selectedAsset,
                State = UpdateState.New
            });
            _latestUpdateStateBySubscriptionId[updatedSubscription.Id] = ToUpdateStateText(UpdateState.New);

            ReplaceSubscriptionInView(updatedSubscription);
            SelectedSubscription = Subscriptions.FirstOrDefault(s => string.Equals(s.Id, updatedSubscription.Id, StringComparison.OrdinalIgnoreCase));
            StatusMessage = $"已添加 {updatedSubscription.FullName}，最新版本 {updatedSubscription.LastSeenTag}，请勾选后点击“安装所选项目”。{BuildPatHint(hasToken)}";
        }
        catch (GitHubApiRateLimitException ex)
        {
            StatusMessage = $"已添加 {subscription.FullName}。{BuildRateLimitStatusMessage(ex.HasToken, ex.ResetAtUtc)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"添加软件包失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task InstallSelectedSubscriptionsAsync()
    {
        var targets = GetCheckedSubscriptions();
        if (targets.Count == 0)
        {
            StatusMessage = "请先勾选要安装的软件包。";
            return;
        }

        IsLoading = true;
        var installedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var failedNames = new List<string>();

        try
        {
            await _store.InitializeAsync();
            var token = await AppServices.GetGitHubTokenAsync();
            var hasToken = !string.IsNullOrWhiteSpace(token);
            var client = AppServices.CreateGitHubClient(token);
            var selector = new AssetSelector();
            var coordinator = new InstallCoordinator();

            foreach (var item in targets)
            {
                var subscription = await _store.GetSubscriptionAsync(item.Id) ?? item;
                try
                {
                    var checkedAtUtc = DateTimeOffset.UtcNow;
                    var release = await client.GetLatestReleaseAsync(
                        new RepositoryId(subscription.Owner, subscription.Repo),
                        subscription.IncludePrerelease);
                    if (release is null)
                    {
                        await _store.UpsertSubscriptionAsync(subscription with { LastCheckedAtUtc = checkedAtUtc });
                        skippedCount++;
                        continue;
                    }

                    var updatedSubscription = subscription with
                    {
                        LastSeenReleaseId = release.Id,
                        LastSeenTag = release.TagName,
                        LastCheckedAtUtc = checkedAtUtc
                    };
                    _latestReleasePublishedAtBySubscriptionId[updatedSubscription.Id] = release.PublishedAt;

                    var selectedAsset = selector.Select(release, updatedSubscription.AssetRules);
                    if (selectedAsset is null)
                    {
                        await _store.AddUpdateEventAsync(new UpdateEvent
                        {
                            SubscriptionId = updatedSubscription.Id,
                            ReleaseId = release.Id,
                            Tag = release.TagName,
                            Title = release.Name,
                            PublishedAtUtc = release.PublishedAt,
                            HtmlUrl = release.HtmlUrl,
                            BodyMarkdown = release.Body,
                            SelectedAsset = null,
                            State = UpdateState.New
                        });
                        await _store.UpsertSubscriptionAsync(updatedSubscription);
                        skippedCount++;
                        continue;
                    }

                    var pipelineResult = await coordinator.ProcessAsync(updatedSubscription, release, selectedAsset);
                    await _store.AddUpdateEventAsync(new UpdateEvent
                    {
                        SubscriptionId = updatedSubscription.Id,
                        ReleaseId = release.Id,
                        Tag = release.TagName,
                        Title = release.Name,
                        PublishedAtUtc = release.PublishedAt,
                        HtmlUrl = release.HtmlUrl,
                        BodyMarkdown = release.Body,
                        SelectedAsset = selectedAsset,
                        State = pipelineResult.State,
                        DownloadedFilePath = pipelineResult.DownloadedFilePath,
                        ScriptPath = pipelineResult.ScriptPath,
                        ScriptExitCode = pipelineResult.ScriptResult?.ExitCode,
                        ScriptStandardOutput = TrimLog(pipelineResult.ScriptResult?.StandardOutput),
                        ScriptStandardError = TrimLog(pipelineResult.ScriptResult?.StandardError),
                        InstallExitCode = pipelineResult.InstallResult?.ExitCode,
                        InstallStandardOutput = TrimLog(pipelineResult.InstallResult?.StandardOutput),
                        InstallStandardError = TrimLog(pipelineResult.InstallResult?.StandardError),
                        ProcessingMessage = pipelineResult.Message,
                        ProcessedAtUtc = DateTimeOffset.UtcNow
                    });
                    await _store.UpsertSubscriptionAsync(updatedSubscription);

                    if (pipelineResult.State == UpdateState.Failed)
                    {
                        failedCount++;
                        failedNames.Add(updatedSubscription.DisplayTitle);
                    }
                    else
                    {
                        installedCount++;
                    }
                }
                catch (GitHubApiRateLimitException ex)
                {
                    await RefreshAsync();
                    StatusMessage = BuildRateLimitStatusMessage(ex.HasToken, ex.ResetAtUtc);
                    return;
                }
                catch
                {
                    failedCount++;
                    failedNames.Add(subscription.DisplayTitle);
                }
            }

            await RefreshAsync();
            var failedList = failedNames.Count == 0 ? string.Empty : $" 失败项目: {string.Join("、", failedNames)}";
            StatusMessage = $"安装完成：成功 {installedCount}，失败 {failedCount}，跳过 {skippedCount}。{BuildPatHint(hasToken)}{failedList}".Trim();
        }
        catch (Exception ex)
        {
            StatusMessage = $"批量安装失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSubscriptionAsync(Subscription? subscription)
    {
        if (subscription is null)
        {
            return;
        }

        try
        {
            await _store.DeleteSubscriptionAsync(subscription.Id);
            if (_ignoredReleaseBySubscriptionId.Remove(subscription.Id))
            {
                await SaveIgnoredUpdatesAsync();
            }

            _installedVersionBySubscriptionId.Remove(subscription.Id);
            Subscriptions.Remove(subscription);
            ApplySubscriptionView();
            if (SelectedSubscription is not null &&
                string.Equals(SelectedSubscription.Id, subscription.Id, StringComparison.OrdinalIgnoreCase))
            {
                SelectedSubscription = null;
            }

            StatusMessage = $"已删除 {subscription.FullName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除软件包失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportSubscriptionsJsonAsync()
    {
        try
        {
            await _store.InitializeAsync();
            var subscriptions = (await _store.GetSubscriptionsAsync())
                .OrderBy(s => s.DisplayTitle)
                .ToList();
            var targetPath = GetConfiguredConfigFilePath();
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new SubscriptionBackupDocument
            {
                Subscriptions = subscriptions,
                ExportedAtUtc = DateTimeOffset.UtcNow
            };
            var json = JsonSerializer.Serialize(document, BackupJsonOptions);
            await File.WriteAllTextAsync(targetPath, json);
            ConfigFilePathInput = targetPath;
            StatusMessage = $"已导出 {subscriptions.Count} 个软件包到 {targetPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出软件包失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ImportSubscriptionsJsonAsync()
    {
        var sourcePath = GetConfiguredConfigFilePath();
        if (!File.Exists(sourcePath))
        {
            StatusMessage = $"未找到配置文件: {sourcePath}";
            return;
        }

        try
        {
            await _store.InitializeAsync();
            var json = await File.ReadAllTextAsync(sourcePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                StatusMessage = "配置文件为空。";
                return;
            }

            var imported = ParseImportedSubscriptions(json);
            if (imported.Count == 0)
            {
                StatusMessage = "配置文件中没有可导入的软件包。";
                return;
            }

            var existing = await _store.GetSubscriptionsAsync();
            var byId = existing.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            var byRepo = existing.ToDictionary(s => GetRepoKey(s.Owner, s.Repo), StringComparer.OrdinalIgnoreCase);

            var applied = 0;
            var skipped = 0;
            foreach (var item in imported)
            {
                var normalized = NormalizeImportedSubscription(item);
                if (normalized is null)
                {
                    skipped++;
                    continue;
                }

                var target = normalized;
                if (!byId.ContainsKey(target.Id))
                {
                    var repoKey = GetRepoKey(target.Owner, target.Repo);
                    if (byRepo.TryGetValue(repoKey, out var sameRepo))
                    {
                        target = target with { Id = sameRepo.Id };
                    }
                }

                await _store.UpsertSubscriptionAsync(target);
                byId[target.Id] = target;
                byRepo[GetRepoKey(target.Owner, target.Repo)] = target;
                applied++;
            }

            await RefreshAsync();
            ConfigFilePathInput = sourcePath;
            StatusMessage = $"导入完成: 应用 {applied}，跳过 {skipped}。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"导入软件包失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadSubscriptionConfig(Subscription? subscription)
    {
        if (subscription is null)
        {
            return;
        }

        SelectedSubscription = subscription;
        StatusMessage = $"已载入 {subscription.FullName} 的配置。";
    }

    [RelayCommand]
    private async Task SaveSubscriptionConfigAsync(Subscription? subscription)
    {
        if (subscription is null)
        {
            StatusMessage = "请选择要保存配置的软件包。";
            return;
        }

        SelectedSubscription = subscription;
        await SaveSelectedSubscriptionAsync();
    }

    [RelayCommand]
    private async Task SaveSelectedSubscriptionAsync()
    {
        if (SelectedSubscription is null)
        {
            StatusMessage = "请先从列表中选择一个软件包。";
            return;
        }

        if (!TryParseTimeoutSeconds(TimeoutSecondsInput, out var timeoutSeconds))
        {
            StatusMessage = "超时时间必须是 1~86400 的整数（秒）。";
            return;
        }

        if (!Enum.TryParse<InstallKind>(InstallKindInput, true, out var installKind))
        {
            StatusMessage = "安装类型无效。可用值: Auto, Msix, Msi, Exe, None。";
            return;
        }

        var updated = SelectedSubscription with
        {
            DisplayName = NormalizeNullable(EditDisplayNameInput),
            IncludePrerelease = EditIncludePrereleaseInput,
            AssetRules = new AssetRuleSet
            {
                Include = ParseInputList(IncludeRulesInput),
                Exclude = ParseInputList(ExcludeRulesInput),
                PreferExtensions = ParseInputList(PreferExtensionsInput),
                PreferArch = ParseInputList(PreferArchInput),
                PreferKeywords = ParseInputList(PreferKeywordsInput)
            },
            InstallKind = installKind,
            SilentArgs = NormalizeNullable(SilentArgsInput),
            MsiArgs = NormalizeNullable(MsiArgsInput),
            RequireAdmin = RequireAdminInput,
            TimeoutSeconds = timeoutSeconds,
            AllowReboot = AllowRebootInput,
            ExpectedPublisher = NormalizeNullable(ExpectedPublisherInput),
            PreInstallScriptEnabled = PreInstallScriptEnabledInput,
            PreInstallScriptPath = NormalizeNullable(PreInstallScriptPathInput),
            PreInstallScriptArgs = NormalizeNullable(PreInstallScriptArgsInput),
            PreInstallScriptRequireAdmin = PreInstallScriptRequireAdminInput
        };

        try
        {
            await _store.UpsertSubscriptionAsync(updated);
            var sorted = Subscriptions
                .Where(s => !string.Equals(s.Id, updated.Id, StringComparison.OrdinalIgnoreCase))
                .Append(updated)
                .OrderBy(s => s.DisplayTitle)
                .ToList();
            Subscriptions = new ObservableCollection<Subscription>(sorted);
            SelectedSubscription = Subscriptions.FirstOrDefault(s => string.Equals(s.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
            StatusMessage = $"已保存 {updated.FullName} 的规则和安装参数。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存软件包配置失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetSelectedSubscriptionEditor()
    {
        if (SelectedSubscription is null)
        {
            StatusMessage = "请先从列表中选择一个软件包。";
            return;
        }

        LoadEditorFromSubscription(SelectedSubscription);
        StatusMessage = $"已还原 {SelectedSubscription.FullName} 的编辑内容。";
    }

    [RelayCommand]
    private void OpenProjectScriptDirectory()
    {
        if (SelectedSubscription is null)
        {
            StatusMessage = "请先从列表中选择一个软件包。";
            return;
        }

        try
        {
            var scriptPath = StorePaths.GetProjectScriptPath(SelectedSubscription.Owner, SelectedSubscription.Repo);
            var directory = Path.GetDirectoryName(scriptPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                StatusMessage = "项目脚本目录无效。";
                return;
            }

            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{directory}\"",
                UseShellExecute = true
            });

            StatusMessage = $"已打开项目脚本目录: {directory}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开项目脚本目录失败: {ex.Message}";
        }
    }

    private void LoadEditorFromSubscription(Subscription? subscription)
    {
        if (subscription is null)
        {
            EditDisplayNameInput = string.Empty;
            EditIncludePrereleaseInput = false;
            IncludeRulesInput = string.Empty;
            ExcludeRulesInput = string.Empty;
            PreferExtensionsInput = string.Empty;
            PreferArchInput = string.Empty;
            PreferKeywordsInput = string.Empty;
            InstallKindInput = InstallKind.Auto.ToString();
            SilentArgsInput = string.Empty;
            MsiArgsInput = string.Empty;
            RequireAdminInput = false;
            TimeoutSecondsInput = "1800";
            AllowRebootInput = false;
            ExpectedPublisherInput = string.Empty;
            PreInstallScriptEnabledInput = false;
            PreInstallScriptPathInput = string.Empty;
            PreInstallScriptArgsInput = string.Empty;
            PreInstallScriptRequireAdminInput = false;
            return;
        }

        EditDisplayNameInput = subscription.DisplayName ?? string.Empty;
        EditIncludePrereleaseInput = subscription.IncludePrerelease;
        IncludeRulesInput = JoinInputList(subscription.AssetRules.Include);
        ExcludeRulesInput = JoinInputList(subscription.AssetRules.Exclude);
        PreferExtensionsInput = JoinInputList(subscription.AssetRules.PreferExtensions);
        PreferArchInput = JoinInputList(subscription.AssetRules.PreferArch);
        PreferKeywordsInput = JoinInputList(subscription.AssetRules.PreferKeywords);
        InstallKindInput = subscription.InstallKind.ToString();
        SilentArgsInput = subscription.SilentArgs ?? string.Empty;
        MsiArgsInput = subscription.MsiArgs ?? string.Empty;
        RequireAdminInput = subscription.RequireAdmin;
        TimeoutSecondsInput = Math.Clamp(subscription.TimeoutSeconds, 1, 86400).ToString(CultureInfo.InvariantCulture);
        AllowRebootInput = subscription.AllowReboot;
        ExpectedPublisherInput = subscription.ExpectedPublisher ?? string.Empty;
        PreInstallScriptEnabledInput = subscription.PreInstallScriptEnabled;
        PreInstallScriptPathInput = subscription.PreInstallScriptPath ?? string.Empty;
        PreInstallScriptArgsInput = subscription.PreInstallScriptArgs ?? string.Empty;
        PreInstallScriptRequireAdminInput = subscription.PreInstallScriptRequireAdmin;
    }

    private static bool TryParseTimeoutSeconds(string text, out int timeoutSeconds)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out timeoutSeconds))
        {
            return false;
        }

        return timeoutSeconds is >= 1 and <= 86400;
    }

    private static List<string> ParseInputList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return text
            .Split(InputSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string JoinInputList(IEnumerable<string> items)
    {
        return string.Join(", ", items.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string GetConfiguredConfigFilePath()
    {
        var path = ConfigFilePathInput.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            path = GetDefaultConfigFilePath();
            ConfigFilePathInput = path;
        }

        return path;
    }

    private static string GetDefaultConfigFilePath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            documents = AppContext.BaseDirectory;
        }

        return Path.Combine(documents, "GithubGet", "subscriptions.json");
    }

    private static List<Subscription> ParseImportedSubscriptions(string json)
    {
        try
        {
            var document = JsonSerializer.Deserialize<SubscriptionBackupDocument>(json, BackupJsonOptions);
            if (document?.Subscriptions is { Count: > 0 })
            {
                return document.Subscriptions;
            }
        }
        catch
        {
        }

        var list = JsonSerializer.Deserialize<List<Subscription>>(json, BackupJsonOptions);
        return list ?? new List<Subscription>();
    }

    private static Subscription? NormalizeImportedSubscription(Subscription item)
    {
        var owner = item.Owner.Trim();
        var repo = item.Repo.Trim();
        if (string.IsNullOrWhiteSpace(owner) && repo.Contains('/'))
        {
            var parsed = RepositoryId.Parse(repo);
            owner = parsed.Owner;
            repo = parsed.Repo;
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            return null;
        }

        var timeoutSeconds = item.TimeoutSeconds is >= 1 and <= 86400 ? item.TimeoutSeconds : 1800;
        var rules = item.AssetRules ?? AssetRuleSet.Default;
        return item with
        {
            Owner = owner,
            Repo = repo,
            DisplayName = NormalizeNullable(item.DisplayName),
            AssetRules = new AssetRuleSet
            {
                Include = NormalizeList(rules.Include),
                Exclude = NormalizeList(rules.Exclude),
                PreferExtensions = NormalizeList(rules.PreferExtensions),
                PreferArch = NormalizeList(rules.PreferArch),
                PreferKeywords = NormalizeList(rules.PreferKeywords)
            },
            SilentArgs = NormalizeNullable(item.SilentArgs),
            MsiArgs = NormalizeNullable(item.MsiArgs),
            TimeoutSeconds = timeoutSeconds,
            ExpectedPublisher = NormalizeNullable(item.ExpectedPublisher),
            PreInstallScriptPath = NormalizeNullable(item.PreInstallScriptPath),
            PreInstallScriptArgs = NormalizeNullable(item.PreInstallScriptArgs)
        };
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return new List<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetRepoKey(string owner, string repo)
    {
        return $"{owner.Trim()}/{repo.Trim()}";
    }

    private void ReplaceSubscriptionInView(Subscription updated)
    {
        var sorted = Subscriptions
            .Where(item => !string.Equals(item.Id, updated.Id, StringComparison.OrdinalIgnoreCase))
            .Append(updated)
            .OrderBy(item => item.DisplayTitle)
            .ToList();
        Subscriptions = new ObservableCollection<Subscription>(sorted);
    }

    private static string BuildRateLimitStatusMessage(bool hasToken, DateTimeOffset? resetAtUtc)
    {
        var resetMessage = resetAtUtc.HasValue
            ? $"（预计恢复时间：{resetAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}）"
            : string.Empty;
        var baseMessage = $"已超出 GitHub API 访问速率，请稍后再试{resetMessage}。";
        return $"{baseMessage} {BuildPatHint(hasToken)}".Trim();
    }

    private static string? TrimLog(string? text, int maxLength = 32_768)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string BuildPatHint(bool hasToken)
    {
        return hasToken ? string.Empty : "未配置 PAT，填写 PAT 可以提高浏览速率。";
    }

    private SubscriptionListItem BuildVisibleSubscriptionItem(Subscription subscription, bool isChecked)
    {
        var latestVersion = string.IsNullOrWhiteSpace(subscription.LastSeenTag) ? "-" : subscription.LastSeenTag!;
        var currentVersion = _installedVersionBySubscriptionId.TryGetValue(subscription.Id, out var detectedVersion)
            ? detectedVersion
            : "未知";
        var newVersion = latestVersion;
        var lastUpdatedAtUtc = _latestReleasePublishedAtBySubscriptionId.TryGetValue(subscription.Id, out var publishedAtUtc)
            ? publishedAtUtc
            : subscription.LastCheckedAtUtc;
        var latestStateText = _latestUpdateStateBySubscriptionId.TryGetValue(subscription.Id, out var stateText)
            ? stateText
            : "未检查";
        return new SubscriptionListItem(subscription, latestVersion, currentVersion, newVersion, "GitHub Release", lastUpdatedAtUtc, latestStateText, isChecked);
    }

    private static string ToUpdateStateText(UpdateState state)
    {
        return state switch
        {
            UpdateState.New => "发现更新",
            UpdateState.Notified => "已通知",
            UpdateState.Downloaded => "已下载",
            UpdateState.Installed => "已安装",
            UpdateState.Failed => "安装失败",
            _ => "未知"
        };
    }

    private void SetInstalledSearchMode(InstalledSearchMode mode)
    {
        if (_installedSearchMode == mode)
        {
            return;
        }

        _installedSearchMode = mode;
        OnPropertyChanged(nameof(InstalledSearchModeOwnerInput));
        OnPropertyChanged(nameof(InstalledSearchModeRepoInput));
        OnPropertyChanged(nameof(InstalledSearchModeOwnerOrRepoInput));
        ApplySubscriptionView();
    }

    private void SetInstalledViewMode(InstalledViewMode mode)
    {
        if (_installedViewMode == mode)
        {
            return;
        }

        _installedViewMode = mode;
        OnPropertyChanged(nameof(InstalledViewModeDetailedInput));
        OnPropertyChanged(nameof(InstalledViewModeCompactInput));
        OnPropertyChanged(nameof(InstalledViewModeCardInput));
        OnPropertyChanged(nameof(InstalledTableHeaderVisibility));
        OnPropertyChanged(nameof(InstalledDetailedViewVisibility));
        OnPropertyChanged(nameof(InstalledCompactViewVisibility));
        OnPropertyChanged(nameof(InstalledCardViewVisibility));
    }

    private void SetUpdateSearchMode(UpdateSearchMode mode)
    {
        if (_updateSearchMode == mode)
        {
            return;
        }

        _updateSearchMode = mode;
        OnPropertyChanged(nameof(UpdateSearchModeOwnerInput));
        OnPropertyChanged(nameof(UpdateSearchModeRepoInput));
        OnPropertyChanged(nameof(UpdateSearchModeOwnerOrRepoInput));
        ApplySubscriptionView();
    }

    private void SetUpdateViewMode(UpdateViewMode mode)
    {
        if (_updateViewMode == mode)
        {
            return;
        }

        _updateViewMode = mode;
        OnPropertyChanged(nameof(UpdateViewModeDetailedInput));
        OnPropertyChanged(nameof(UpdateViewModeCompactInput));
        OnPropertyChanged(nameof(UpdateViewModeCardInput));
        OnPropertyChanged(nameof(UpdateTableHeaderVisibility));
        OnPropertyChanged(nameof(UpdateDetailedViewVisibility));
        OnPropertyChanged(nameof(UpdateCompactViewVisibility));
        OnPropertyChanged(nameof(UpdateCardViewVisibility));
    }

    private bool MatchesInstalledSearch(Subscription subscription, string keyword)
    {
        var normalizedKeyword = NormalizeInstalledSearchValue(keyword);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return true;
        }

        var comparison = IsCaseSensitiveSearchInput ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var candidate in GetInstalledSearchCandidates(subscription))
        {
            var normalizedCandidate = NormalizeInstalledSearchValue(candidate);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                continue;
            }

            var matched = normalizedCandidate.Contains(normalizedKeyword, comparison);
            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetInstalledSearchCandidates(Subscription subscription)
    {
        return _installedSearchMode switch
        {
            InstalledSearchMode.Owner => new[] { subscription.Owner },
            InstalledSearchMode.Repo => new[] { subscription.Repo },
            InstalledSearchMode.OwnerOrRepo => new[]
            {
                subscription.Owner,
                subscription.Repo,
            },
            _ => new[] { subscription.Owner, subscription.Repo }
        };
    }

    private string NormalizeInstalledSearchValue(string value)
    {
        var trimmed = value.Trim();
        if (!IsIgnoreSpecialCharsSearchInput)
        {
            return trimmed;
        }

        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private bool MatchesUpdateSearch(Subscription subscription, string keyword)
    {
        var normalizedKeyword = NormalizeUpdateSearchValue(keyword);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return true;
        }

        var comparison = IsUpdateCaseSensitiveSearchInput ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        foreach (var candidate in GetUpdateSearchCandidates(subscription))
        {
            var normalizedCandidate = NormalizeUpdateSearchValue(candidate);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
            {
                continue;
            }

            var matched = normalizedCandidate.Contains(normalizedKeyword, comparison);
            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetUpdateSearchCandidates(Subscription subscription)
    {
        return _updateSearchMode switch
        {
            UpdateSearchMode.Owner => new[] { subscription.Owner },
            UpdateSearchMode.Repo => new[] { subscription.Repo },
            UpdateSearchMode.OwnerOrRepo => new[]
            {
                subscription.Owner,
                subscription.Repo,
            },
            _ => new[] { subscription.Owner, subscription.Repo }
        };
    }

    private string NormalizeUpdateSearchValue(string value)
    {
        var trimmed = value.Trim();
        if (!IsUpdateIgnoreSpecialCharsSearchInput)
        {
            return trimmed;
        }

        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private async Task LoadIgnoredUpdatesAsync()
    {
        try
        {
            var value = await _store.GetSettingAsync(IgnoredUpdatesSettingKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                _ignoredReleaseBySubscriptionId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, long>>(value, BackupJsonOptions) ??
                         new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            _ignoredReleaseBySubscriptionId = parsed
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value > 0)
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            _ignoredReleaseBySubscriptionId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private Task SaveIgnoredUpdatesAsync()
    {
        var json = JsonSerializer.Serialize(_ignoredReleaseBySubscriptionId, BackupJsonOptions);
        return _store.SetSettingAsync(IgnoredUpdatesSettingKey, json);
    }

    private List<Subscription> GetCheckedSubscriptions()
    {
        return VisibleSubscriptions
            .Concat(VisibleUpdateSubscriptions)
            .Where(item => item.IsChecked)
            .GroupBy(item => item.Subscription.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Subscription)
            .ToList();
    }

    private bool IsLatestReleaseIgnored(Subscription subscription)
    {
        if (!subscription.LastSeenReleaseId.HasValue)
        {
            return false;
        }

        return _ignoredReleaseBySubscriptionId.TryGetValue(subscription.Id, out var ignoredReleaseId) &&
               ignoredReleaseId == subscription.LastSeenReleaseId.Value;
    }

    private bool ShouldDisplayInUpdates(Subscription subscription)
    {
        if (!subscription.LastSeenReleaseId.HasValue)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(subscription.LastSeenTag))
        {
            return false;
        }

        if (_latestUpdateStateBySubscriptionId.TryGetValue(subscription.Id, out var stateText) &&
            string.Equals(stateText, ToUpdateStateText(UpdateState.Installed), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsLatestReleaseIgnored(subscription))
        {
            return false;
        }

        if (IsInstalledOnLatestVersion(subscription))
        {
            return false;
        }

        return true;
    }

    private void ApplySubscriptionView()
    {
        var selectedMap = VisibleSubscriptions
            .Concat(VisibleUpdateSubscriptions)
            .GroupBy(item => item.Subscription.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Any(item => item.IsChecked),
                StringComparer.OrdinalIgnoreCase);

        IEnumerable<Subscription> installedQuery = Subscriptions;
        var installedKeyword = SubscriptionSearchInput.Trim();
        if (!string.IsNullOrWhiteSpace(installedKeyword))
        {
            installedQuery = installedQuery.Where(subscription => MatchesInstalledSearch(subscription, installedKeyword));
        }

        installedQuery = SubscriptionSortInput switch
        {
            "仓库" => installedQuery.OrderBy(subscription => subscription.FullName, StringComparer.OrdinalIgnoreCase),
            "最近检查" => installedQuery
                .OrderByDescending(subscription => subscription.LastCheckedAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(subscription => subscription.DisplayTitle, StringComparer.OrdinalIgnoreCase),
            _ => installedQuery.OrderBy(subscription => subscription.DisplayTitle, StringComparer.OrdinalIgnoreCase)
        };

        var installedItems = installedQuery.Select(subscription =>
        {
            var isChecked = selectedMap.TryGetValue(subscription.Id, out var checkedValue) && checkedValue;
            return BuildVisibleSubscriptionItem(subscription, isChecked);
        });
        VisibleSubscriptions = new ObservableCollection<SubscriptionListItem>(installedItems);

        IEnumerable<Subscription> updateQuery = Subscriptions
            .Where(ShouldDisplayInUpdates);
        var updateKeyword = UpdateSearchInput.Trim();
        if (!string.IsNullOrWhiteSpace(updateKeyword))
        {
            updateQuery = updateQuery.Where(subscription => MatchesUpdateSearch(subscription, updateKeyword));
        }

        updateQuery = UpdateSortInput switch
        {
            "仓库" => updateQuery.OrderBy(subscription => subscription.FullName, StringComparer.OrdinalIgnoreCase),
            "最近检查" => updateQuery
                .OrderByDescending(subscription => subscription.LastCheckedAtUtc ?? DateTimeOffset.MinValue)
                .ThenBy(subscription => subscription.DisplayTitle, StringComparer.OrdinalIgnoreCase),
            _ => updateQuery.OrderBy(subscription => subscription.DisplayTitle, StringComparer.OrdinalIgnoreCase)
        };

        var updateItems = updateQuery
            .Select(subscription =>
            {
                var isChecked = selectedMap.TryGetValue(subscription.Id, out var checkedValue) && checkedValue;
                return BuildVisibleSubscriptionItem(subscription, isChecked);
            });
        VisibleUpdateSubscriptions = new ObservableCollection<SubscriptionListItem>(updateItems);
    }

    private bool IsInstalledOnLatestVersion(Subscription subscription)
    {
        if (!_installedVersionBySubscriptionId.TryGetValue(subscription.Id, out var installedVersion))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(installedVersion) ||
            string.Equals(installedVersion, "未知", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedInstalledVersion = NormalizeVersionForCompare(installedVersion);
        var normalizedLatestVersion = NormalizeVersionForCompare(subscription.LastSeenTag ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedInstalledVersion) ||
            string.IsNullOrWhiteSpace(normalizedLatestVersion))
        {
            return false;
        }

        return string.Equals(normalizedInstalledVersion, normalizedLatestVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersionForCompare(string version)
    {
        var normalized = version.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }

    private sealed record SubscriptionBackupDocument
    {
        public int Version { get; init; } = 1;
        public DateTimeOffset ExportedAtUtc { get; init; } = DateTimeOffset.UtcNow;
        public List<Subscription> Subscriptions { get; init; } = new();
    }
}
