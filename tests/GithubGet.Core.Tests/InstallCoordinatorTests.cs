using System.Net;
using System.Text;
using GithubGet.Core.Installers;
using GithubGet.Core.Models;
using GithubGet.Core.Services;
using GithubGet.Core.Storage;

namespace GithubGet.Core.Tests;

public class InstallCoordinatorTests
{
    [Fact]
    public async Task ProcessAsync_RunsScriptBeforeInstaller()
    {
        var scriptRunner = new RecordingScriptRunner(CreateScriptResult(success: true));
        var installer = new RecordingInstaller(scriptRunner, CreateInstallResult(success: true));
        using var httpClient = new HttpClient(new StaticContentHandler("installer-content"));
        var coordinator = new InstallCoordinator(httpClient, scriptRunner, installer);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid().ToString("N"),
            Owner = "owner",
            Repo = "repo",
            InstallKind = InstallKind.Exe,
            PreInstallScriptEnabled = true,
            PreInstallScriptPath = "pre-install.ps1"
        };

        var release = new ReleaseInfo(
            1,
            "v1.0.0",
            "Release",
            DateTimeOffset.UtcNow,
            false,
            false,
            "https://example.com/release",
            null,
            new[]
            {
                new ReleaseAsset("setup.exe", 10, "https://example.com/setup.exe", "application/octet-stream")
            });

        var selectedAsset = new SelectedAsset(release.Assets[0], InstallKind.Exe, 100);
        var result = await coordinator.ProcessAsync(subscription, release, selectedAsset);

        Assert.Equal(UpdateState.Installed, result.State);
        Assert.True(scriptRunner.Called);
        Assert.True(installer.Called);
        Assert.True(installer.SawScriptExecuted);
        Assert.NotNull(result.DownloadedFilePath);
        Assert.True(File.Exists(result.DownloadedFilePath!));
        Assert.Equal(result.DownloadedFilePath, installer.LastRequest?.FilePath);
        Assert.NotNull(scriptRunner.LastRequest);
        Assert.Equal(result.DownloadedFilePath, scriptRunner.LastRequest!.Context.DownloadedFilePath);

        CleanupDownloadedFile(result.DownloadedFilePath);
    }

    [Fact]
    public async Task ProcessAsync_ScriptFailure_BlocksInstaller()
    {
        var scriptRunner = new RecordingScriptRunner(CreateScriptResult(success: false));
        var installer = new RecordingInstaller(scriptRunner, CreateInstallResult(success: true));
        using var httpClient = new HttpClient(new StaticContentHandler("installer-content"));
        var coordinator = new InstallCoordinator(httpClient, scriptRunner, installer);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid().ToString("N"),
            Owner = "owner",
            Repo = "repo",
            InstallKind = InstallKind.Exe,
            PreInstallScriptEnabled = true,
            PreInstallScriptPath = "pre-install.ps1"
        };

        var release = new ReleaseInfo(
            1,
            "v2.0.0",
            "Release",
            DateTimeOffset.UtcNow,
            false,
            false,
            "https://example.com/release",
            null,
            new[]
            {
                new ReleaseAsset("setup.exe", 10, "https://example.com/setup.exe", "application/octet-stream")
            });

        var selectedAsset = new SelectedAsset(release.Assets[0], InstallKind.Exe, 100);
        var result = await coordinator.ProcessAsync(subscription, release, selectedAsset);

        Assert.Equal(UpdateState.Failed, result.State);
        Assert.True(scriptRunner.Called);
        Assert.False(installer.Called);
        Assert.NotNull(result.DownloadedFilePath);
        Assert.True(File.Exists(result.DownloadedFilePath!));

        CleanupDownloadedFile(result.DownloadedFilePath);
    }

    [Fact]
    public async Task ProcessAsync_UsesProjectDefaultScriptPathWhenOverrideIsEmpty()
    {
        var scriptRunner = new RecordingScriptRunner(CreateScriptResult(success: true));
        var installer = new RecordingInstaller(scriptRunner, CreateInstallResult(success: true));
        using var httpClient = new HttpClient(new StaticContentHandler("installer-content"));
        var coordinator = new InstallCoordinator(httpClient, scriptRunner, installer);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid().ToString("N"),
            Owner = "owner",
            Repo = "repo",
            InstallKind = InstallKind.Exe,
            PreInstallScriptEnabled = true,
            PreInstallScriptPath = null
        };

        var release = new ReleaseInfo(
            1,
            "v3.0.0",
            "Release",
            DateTimeOffset.UtcNow,
            false,
            false,
            "https://example.com/release",
            null,
            new[]
            {
                new ReleaseAsset("setup.exe", 10, "https://example.com/setup.exe", "application/octet-stream")
            });

        var selectedAsset = new SelectedAsset(release.Assets[0], InstallKind.Exe, 100);
        var result = await coordinator.ProcessAsync(subscription, release, selectedAsset);

        Assert.Equal(UpdateState.Installed, result.State);
        Assert.NotNull(scriptRunner.LastRequest);
        Assert.Equal(
            StorePaths.GetProjectScriptPath(subscription.Owner, subscription.Repo),
            scriptRunner.LastRequest!.ScriptPath);

        CleanupDownloadedFile(result.DownloadedFilePath);
    }

    private static InstallResult CreateInstallResult(bool success)
    {
        return new InstallResult
        {
            Succeeded = success,
            ExitCode = success ? 0 : 1,
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow,
            Message = success ? null : "Install failed."
        };
    }

    private static ScriptResult CreateScriptResult(bool success)
    {
        return new ScriptResult
        {
            Succeeded = success,
            ExitCode = success ? 0 : 1,
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow,
            Message = success ? null : "Script failed."
        };
    }

    private static void CleanupDownloadedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
        }
    }

    private sealed class RecordingScriptRunner : IScriptRunner
    {
        private readonly ScriptResult _result;

        public RecordingScriptRunner(ScriptResult result)
        {
            _result = result;
        }

        public bool Called { get; private set; }
        public ScriptExecutionRequest? LastRequest { get; private set; }

        public Task<ScriptResult> RunAsync(ScriptExecutionRequest request, CancellationToken ct = default)
        {
            Called = true;
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingInstaller : IInstaller
    {
        private readonly RecordingScriptRunner _scriptRunner;
        private readonly InstallResult _result;

        public RecordingInstaller(RecordingScriptRunner scriptRunner, InstallResult result)
        {
            _scriptRunner = scriptRunner;
            _result = result;
        }

        public bool Called { get; private set; }
        public bool SawScriptExecuted { get; private set; }
        public InstallRequest? LastRequest { get; private set; }

        public Task<InstallResult> InstallAsync(InstallRequest request, CancellationToken ct = default)
        {
            Called = true;
            SawScriptExecuted = _scriptRunner.Called;
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class StaticContentHandler : HttpMessageHandler
    {
        private readonly byte[] _content;

        public StaticContentHandler(string content)
        {
            _content = Encoding.UTF8.GetBytes(content);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_content)
            };
            return Task.FromResult(response);
        }
    }
}
