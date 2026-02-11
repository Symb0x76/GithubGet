using GithubGet.Core.Installers;
using GithubGet.Core.Models;
using GithubGet.Core.Storage;

namespace GithubGet.Core.Services;

public sealed class InstallCoordinator
{
    private readonly HttpClient _httpClient;
    private readonly IScriptRunner _scriptRunner;
    private readonly IInstaller _installer;

    public InstallCoordinator(HttpClient? httpClient = null, IScriptRunner? scriptRunner = null, IInstaller? installer = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _scriptRunner = scriptRunner ?? new PowerShellScriptRunner();
        _installer = installer ?? new ProcessInstaller();
    }

    public async Task<InstallPipelineResult> ProcessAsync(
        Subscription subscription,
        ReleaseInfo release,
        SelectedAsset selectedAsset,
        CancellationToken ct = default)
    {
        var downloadPath = BuildDownloadPath(subscription, release, selectedAsset.Asset);
        await DownloadAssetAsync(selectedAsset.Asset.DownloadUrl, downloadPath, ct);

        ScriptResult? scriptResult = null;
        string? scriptPath = null;
        if (subscription.PreInstallScriptEnabled)
        {
            scriptPath = ResolveScriptPath(subscription);
            var scriptRequest = new ScriptExecutionRequest
            {
                ScriptPath = scriptPath,
                Args = subscription.PreInstallScriptArgs,
                RequireAdmin = subscription.PreInstallScriptRequireAdmin,
                TimeoutSeconds = subscription.TimeoutSeconds,
                Context = new ScriptExecutionContext
                {
                    DownloadedFilePath = downloadPath,
                    DownloadDirectory = Path.GetDirectoryName(downloadPath) ?? string.Empty,
                    ReleaseTag = release.TagName,
                    ReleaseTitle = release.Name,
                    ReleaseUrl = release.HtmlUrl,
                    SubscriptionId = subscription.Id,
                    SubscriptionName = subscription.DisplayTitle
                }
            };
            scriptResult = await _scriptRunner.RunAsync(scriptRequest, ct);
            if (!scriptResult.Succeeded)
            {
                return new InstallPipelineResult
                {
                    State = UpdateState.Failed,
                    DownloadedFilePath = downloadPath,
                    ScriptPath = scriptPath,
                    ScriptResult = scriptResult,
                    Message = scriptResult.Message ?? "Pre-install script failed."
                };
            }
        }

        var effectiveKind = subscription.InstallKind == InstallKind.Auto
            ? selectedAsset.InstallKind
            : subscription.InstallKind;

        if (effectiveKind == InstallKind.None)
        {
            return new InstallPipelineResult
            {
                State = UpdateState.Downloaded,
                DownloadedFilePath = downloadPath,
                ScriptPath = scriptPath,
                ScriptResult = scriptResult,
                Message = "Installer is disabled for this subscription."
            };
        }

        var installRequest = new InstallRequest
        {
            FilePath = downloadPath,
            Kind = effectiveKind,
            Args = effectiveKind == InstallKind.Msi ? subscription.MsiArgs : subscription.SilentArgs,
            RequireAdmin = subscription.RequireAdmin,
            TimeoutSeconds = subscription.TimeoutSeconds,
            AllowReboot = subscription.AllowReboot,
            ExpectedPublisher = subscription.ExpectedPublisher
        };
        var installResult = await _installer.InstallAsync(installRequest, ct);

        return new InstallPipelineResult
        {
            State = installResult.Succeeded ? UpdateState.Installed : UpdateState.Failed,
            DownloadedFilePath = downloadPath,
            ScriptPath = scriptPath,
            ScriptResult = scriptResult,
            InstallResult = installResult,
            Message = installResult.Message
        };
    }

    private async Task DownloadAssetAsync(string downloadUrl, string targetPath, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? StorePaths.CacheRoot);
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination, ct);
    }

    private static string ResolveScriptPath(Subscription subscription)
    {
        var configuredPath = subscription.PreInstallScriptPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return StorePaths.GetProjectScriptPath(subscription.Owner, subscription.Repo);
        }

        var normalized = configuredPath.Trim();
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        return Path.GetFullPath(Path.Combine(StorePaths.ScriptsRoot, normalized));
    }

    private static string BuildDownloadPath(Subscription subscription, ReleaseInfo release, ReleaseAsset asset)
    {
        var sanitizedTag = SanitizePathSegment(string.IsNullOrWhiteSpace(release.TagName) ? "release" : release.TagName);
        var sanitizedName = SanitizePathSegment(asset.Name);
        var directory = Path.Combine(StorePaths.DownloadsRoot, subscription.Id, sanitizedTag);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, sanitizedName);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
}
