using GithubGet.Core.Models;
using GithubGet.Core.Storage;

namespace GithubGet.Core.Services;

public sealed class UpdateChecker
{
    private readonly IGithubGetStore _store;
    private readonly IGitHubClient _client;
    private readonly AssetSelector _assetSelector;
    private readonly InstallCoordinator _installCoordinator;

    public UpdateChecker(
        IGithubGetStore store,
        IGitHubClient client,
        AssetSelector assetSelector,
        InstallCoordinator? installCoordinator = null)
    {
        _store = store;
        _client = client;
        _assetSelector = assetSelector;
        _installCoordinator = installCoordinator ?? new InstallCoordinator();
    }

    public async Task<UpdateCheckSummary> RunOnceAsync(UpdateCheckOptions options, CancellationToken ct = default)
    {
        await _store.InitializeAsync(ct);

        var subscriptions = string.IsNullOrWhiteSpace(options.SubscriptionId)
            ? await _store.GetSubscriptionsAsync(ct)
            : await GetSingleSubscription(options.SubscriptionId, ct);

        var checkedCount = 0;
        var updatedCount = 0;
        var failedCount = 0;
        var rateLimited = false;
        var hasToken = false;
        DateTimeOffset? rateLimitResetAtUtc = null;

        foreach (var subscription in subscriptions)
        {
            checkedCount++;
            try
            {
                var release = await _client.GetLatestReleaseAsync(
                    new RepositoryId(subscription.Owner, subscription.Repo),
                    subscription.IncludePrerelease,
                    ct);

                if (release is null)
                {
                    await _store.UpsertSubscriptionAsync(subscription with { LastCheckedAtUtc = DateTimeOffset.UtcNow }, ct);
                    continue;
                }

                if (subscription.LastSeenReleaseId == release.Id && subscription.LastSeenTag == release.TagName)
                {
                    await _store.UpsertSubscriptionAsync(subscription with { LastCheckedAtUtc = DateTimeOffset.UtcNow }, ct);
                    continue;
                }

                var selected = _assetSelector.Select(release, subscription.AssetRules);
                var updateEvent = new UpdateEvent
                {
                    SubscriptionId = subscription.Id,
                    ReleaseId = release.Id,
                    Tag = release.TagName,
                    Title = release.Name,
                    PublishedAtUtc = release.PublishedAt,
                    HtmlUrl = release.HtmlUrl,
                    BodyMarkdown = release.Body,
                    SelectedAsset = selected,
                    State = UpdateState.New
                };

                if (options.InstallAssets && selected is not null)
                {
                    try
                    {
                        var pipelineResult = await _installCoordinator.ProcessAsync(subscription, release, selected, ct);
                        updateEvent = updateEvent with
                        {
                            State = pipelineResult.State,
                            DownloadedFilePath = pipelineResult.DownloadedFilePath,
                            ScriptPath = pipelineResult.ScriptPath,
                            ScriptExitCode = pipelineResult.ScriptResult?.ExitCode,
                            ScriptStandardOutput = TruncateLog(pipelineResult.ScriptResult?.StandardOutput),
                            ScriptStandardError = TruncateLog(pipelineResult.ScriptResult?.StandardError),
                            InstallExitCode = pipelineResult.InstallResult?.ExitCode,
                            InstallStandardOutput = TruncateLog(pipelineResult.InstallResult?.StandardOutput),
                            InstallStandardError = TruncateLog(pipelineResult.InstallResult?.StandardError),
                            ProcessingMessage = pipelineResult.Message,
                            ProcessedAtUtc = DateTimeOffset.UtcNow
                        };

                        if (pipelineResult.State == UpdateState.Failed)
                        {
                            failedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        updateEvent = updateEvent with
                        {
                            State = UpdateState.Failed,
                            ProcessingMessage = $"Install pipeline failed: {ex.Message}",
                            ProcessedAtUtc = DateTimeOffset.UtcNow
                        };
                        failedCount++;
                    }
                }

                await _store.AddUpdateEventAsync(updateEvent, ct);
                await _store.UpsertSubscriptionAsync(subscription with
                {
                    LastSeenReleaseId = release.Id,
                    LastSeenTag = release.TagName,
                    LastCheckedAtUtc = DateTimeOffset.UtcNow
                }, ct);

                updatedCount++;
            }
            catch (GitHubApiRateLimitException ex)
            {
                failedCount++;
                rateLimited = true;
                hasToken = ex.HasToken;
                rateLimitResetAtUtc = ex.ResetAtUtc;
                break;
            }
            catch
            {
                failedCount++;
            }
        }

        return new UpdateCheckSummary(checkedCount, updatedCount, failedCount, rateLimited, hasToken, rateLimitResetAtUtc);
    }

    private async Task<IReadOnlyList<Subscription>> GetSingleSubscription(string subscriptionId, CancellationToken ct)
    {
        var subscription = await _store.GetSubscriptionAsync(subscriptionId, ct);
        return subscription is null ? Array.Empty<Subscription>() : new[] { subscription };
    }

    private static string? TruncateLog(string? text, int maxLength = 32_768)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
