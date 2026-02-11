using GithubGet.Core.Services;
using GithubGet.Core.Storage;

namespace GithubGet.App.Services;

public static class AppServices
{
    private const string GitHubTokenKey = "github.token.protected";
    private const string WorkerPathKey = "worker.path";
    private static readonly HttpClient HttpClient = new();
    private static readonly SqliteGithubGetStore StoreInstance = new();
    private static readonly TaskSchedulerService TaskScheduler = new();

    public static IGithubGetStore Store => StoreInstance;
    public static TaskSchedulerService Scheduler => TaskScheduler;

    public static GitHubClient CreateGitHubClient(string? token = null)
    {
        return new GitHubClient(HttpClient, new GitHubClientOptions { Token = token }, StoreInstance);
    }

    public static UpdateChecker CreateUpdateChecker(string? token = null)
    {
        return new UpdateChecker(StoreInstance, CreateGitHubClient(token), new AssetSelector());
    }

    public static async Task<string?> GetGitHubTokenAsync()
    {
        var protectedValue = await StoreInstance.GetSettingAsync(GitHubTokenKey);
        return TokenProtector.Unprotect(protectedValue);
    }

    public static async Task SaveGitHubTokenAsync(string? token)
    {
        var protectedValue = TokenProtector.Protect(token);
        await StoreInstance.SetSettingAsync(GitHubTokenKey, protectedValue ?? string.Empty);
    }

    public static async Task<string?> GetWorkerPathAsync()
    {
        var value = await StoreInstance.GetSettingAsync(WorkerPathKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return TaskSchedulerService.FindDefaultWorkerPath();
    }

    public static async Task SaveWorkerPathAsync(string? workerPath)
    {
        await StoreInstance.SetSettingAsync(WorkerPathKey, workerPath?.Trim() ?? string.Empty);
    }
}
