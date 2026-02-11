using Microsoft.UI.Xaml.Controls;

namespace GithubGet.App.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private bool _initialized;
    private bool _suppressAutoCheckApply;
    private string _tokenInput = string.Empty;
    private bool _autoCheckEnabled;
    private bool _isAutoCheckInfoOpen = true;
    private string _autoCheckInfoTitle = "自动检查";
    private string _autoCheckInfoMessage = "每天自动检查更新（默认 09:00）。";
    private InfoBarSeverity _autoCheckInfoSeverity = InfoBarSeverity.Informational;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public SettingsViewModel()
    {
        Title = "设置";
    }

    public string TokenInput
    {
        get => _tokenInput;
        set => SetProperty(ref _tokenInput, value);
    }

    public bool AutoCheckEnabled
    {
        get => _autoCheckEnabled;
        set
        {
            if (!SetProperty(ref _autoCheckEnabled, value))
            {
                return;
            }

            if (_suppressAutoCheckApply || !_initialized)
            {
                return;
            }

            _ = ApplyAutoCheckAsync(value);
        }
    }

    public bool IsAutoCheckInfoOpen
    {
        get => _isAutoCheckInfoOpen;
        set => SetProperty(ref _isAutoCheckInfoOpen, value);
    }

    public string AutoCheckInfoTitle
    {
        get => _autoCheckInfoTitle;
        set => SetProperty(ref _autoCheckInfoTitle, value);
    }

    public string AutoCheckInfoMessage
    {
        get => _autoCheckInfoMessage;
        set => SetProperty(ref _autoCheckInfoMessage, value);
    }

    public InfoBarSeverity AutoCheckInfoSeverity
    {
        get => _autoCheckInfoSeverity;
        set => SetProperty(ref _autoCheckInfoSeverity, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            TokenInput = await AppServices.GetGitHubTokenAsync() ?? string.Empty;
            await RefreshAutoCheckStateAsync();
            StatusMessage = string.IsNullOrWhiteSpace(TokenInput)
                ? "未配置 GitHub PAT Token。"
                : "已加载 GitHub PAT Token。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取设置失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveTokenAsync()
    {
        IsLoading = true;
        try
        {
            await AppServices.SaveGitHubTokenAsync(TokenInput);
            StatusMessage = "设置已保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearTokenAsync()
    {
        IsLoading = true;
        try
        {
            TokenInput = string.Empty;
            await AppServices.SaveGitHubTokenAsync(null);
            StatusMessage = "Token 已清除。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"清除失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RegisterTaskAsync()
    {
        await ApplyAutoCheckAsync(true);
    }

    [RelayCommand]
    private async Task DeleteTaskAsync()
    {
        await ApplyAutoCheckAsync(false);
    }

    [RelayCommand]
    private async Task QueryTaskAsync()
    {
        IsLoading = true;
        try
        {
            StatusMessage = await AppServices.Scheduler.QueryTaskAsync();
            await RefreshAutoCheckStateAsync();
            ShowAutoCheckInfo("自动检查状态", StatusMessage, InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            StatusMessage = $"查询任务失败: {ex.Message}";
            ShowAutoCheckInfo("自动检查状态", StatusMessage, InfoBarSeverity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyAutoCheckAsync(bool enabled)
    {
        IsLoading = true;
        try
        {
            StatusMessage = enabled
                ? await EnableAutoCheckAsync()
                : await AppServices.Scheduler.DeleteTaskAsync();

            await RefreshAutoCheckStateAsync();

            var severity = AutoCheckEnabled == enabled
                ? InfoBarSeverity.Success
                : InfoBarSeverity.Warning;

            var title = enabled ? "启用自动检查" : "关闭自动检查";
            ShowAutoCheckInfo(title, StatusMessage, severity);
        }
        catch (Exception ex)
        {
            StatusMessage = enabled
                ? $"启用自动检查失败: {ex.Message}"
                : $"关闭自动检查失败: {ex.Message}";
            ShowAutoCheckInfo("自动检查错误", StatusMessage, InfoBarSeverity.Error);
            await RefreshAutoCheckStateAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<string> EnableAutoCheckAsync()
    {
        var workerPath = await AppServices.GetWorkerPathAsync();
        return await AppServices.Scheduler.CreateOrUpdateDailyTaskAsync(workerPath ?? string.Empty);
    }

    private async Task RefreshAutoCheckStateAsync()
    {
        var query = await AppServices.Scheduler.QueryTaskStateAsync();
        _suppressAutoCheckApply = true;
        AutoCheckEnabled = query.Success;
        _suppressAutoCheckApply = false;
    }

    private void ShowAutoCheckInfo(string title, string message, InfoBarSeverity severity)
    {
        AutoCheckInfoTitle = title;
        AutoCheckInfoMessage = message;
        AutoCheckInfoSeverity = severity;
        IsAutoCheckInfoOpen = true;
    }
}
