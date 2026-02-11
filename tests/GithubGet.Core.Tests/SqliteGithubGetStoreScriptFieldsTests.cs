using GithubGet.Core.Models;
using GithubGet.Core.Storage;

namespace GithubGet.Core.Tests;

public class SqliteGithubGetStoreScriptFieldsTests
{
    [Fact]
    public async Task UpsertSubscription_PersistsPreInstallScriptFields()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var store = new SqliteGithubGetStore(dbPath);
            var subscription = new Subscription
            {
                Id = Guid.NewGuid().ToString("N"),
                Owner = "owner",
                Repo = "repo",
                PreInstallScriptEnabled = true,
                PreInstallScriptPath = "hook.ps1",
                PreInstallScriptArgs = "-Mode Silent",
                PreInstallScriptRequireAdmin = true
            };

            await store.UpsertSubscriptionAsync(subscription);
            var reloaded = await store.GetSubscriptionAsync(subscription.Id);

            Assert.NotNull(reloaded);
            Assert.True(reloaded!.PreInstallScriptEnabled);
            Assert.Equal("hook.ps1", reloaded.PreInstallScriptPath);
            Assert.Equal("-Mode Silent", reloaded.PreInstallScriptArgs);
            Assert.True(reloaded.PreInstallScriptRequireAdmin);
        }
        finally
        {
            DeleteTempDatabaseFile(dbPath);
        }
    }

    [Fact]
    public async Task AddUpdateEvent_PersistsPipelineLogsAndContext()
    {
        var dbPath = CreateTempDatabasePath();
        try
        {
            var store = new SqliteGithubGetStore(dbPath);
            var updateEvent = new UpdateEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                SubscriptionId = "sub",
                ReleaseId = 10,
                Tag = "v1.0.0",
                PublishedAtUtc = DateTimeOffset.UtcNow,
                HtmlUrl = "https://example.com/release",
                SelectedAsset = new SelectedAsset(
                    new ReleaseAsset("setup.exe", 10, "https://example.com/setup.exe", "application/octet-stream"),
                    InstallKind.Exe,
                    100),
                DownloadedFilePath = @"C:\Temp\setup.exe",
                ScriptPath = @"scripts\hook.ps1",
                ScriptExitCode = 0,
                ScriptStandardOutput = "script ok",
                ScriptStandardError = null,
                InstallExitCode = 0,
                InstallStandardOutput = "install ok",
                InstallStandardError = null,
                ProcessingMessage = "pipeline complete",
                ProcessedAtUtc = DateTimeOffset.UtcNow,
                State = UpdateState.Installed,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            await store.AddUpdateEventAsync(updateEvent);
            var events = await store.GetUpdateEventsAsync(limit: 10);
            var reloaded = events.Single(e => e.Id == updateEvent.Id);

            Assert.Equal(updateEvent.DownloadedFilePath, reloaded.DownloadedFilePath);
            Assert.Equal(updateEvent.ScriptPath, reloaded.ScriptPath);
            Assert.Equal(updateEvent.ScriptExitCode, reloaded.ScriptExitCode);
            Assert.Equal(updateEvent.ScriptStandardOutput, reloaded.ScriptStandardOutput);
            Assert.Equal(updateEvent.InstallExitCode, reloaded.InstallExitCode);
            Assert.Equal(updateEvent.InstallStandardOutput, reloaded.InstallStandardOutput);
            Assert.Equal(updateEvent.ProcessingMessage, reloaded.ProcessingMessage);
            Assert.Equal(UpdateState.Installed, reloaded.State);
        }
        finally
        {
            DeleteTempDatabaseFile(dbPath);
        }
    }

    private static string CreateTempDatabasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "GithubGet.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "githubget.db");
    }

    private static void DeleteTempDatabaseFile(string dbPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
        }
    }
}
