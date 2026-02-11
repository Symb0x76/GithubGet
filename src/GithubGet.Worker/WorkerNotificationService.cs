using CommunityToolkit.WinUI.Notifications;
using GithubGet.Core.Models;
using GithubGet.Core.Storage;

namespace GithubGet.Worker;

public sealed class WorkerNotificationService
{
    private readonly IGithubGetStore _store;

    public WorkerNotificationService(IGithubGetStore store)
    {
        _store = store;
    }

    public async Task<int> NotifyPendingUpdatesAsync(CancellationToken ct = default)
    {
        var subscriptions = await _store.GetSubscriptionsAsync(ct);
        var nameMap = subscriptions.ToDictionary(s => s.Id, s => s.DisplayTitle);
        var updateEvents = await _store.GetUpdateEventsAsync(limit: 200, ct: ct);
        var pending = updateEvents.Where(e => e.State == UpdateState.New).Take(5).ToList();

        var notified = 0;
        foreach (var updateEvent in pending)
        {
            var name = nameMap.TryGetValue(updateEvent.SubscriptionId, out var value) ? value : updateEvent.SubscriptionId;
            var subtitle = string.IsNullOrWhiteSpace(updateEvent.Title) ? updateEvent.Tag : $"{updateEvent.Tag} - {updateEvent.Title}";

            if (TryShowToast(name, subtitle))
            {
                await _store.UpdateUpdateEventStateAsync(updateEvent.Id, UpdateState.Notified, ct);
                notified++;
            }
        }

        return notified;
    }

    private static bool TryShowToast(string title, string subtitle)
    {
        try
        {
            new ToastContentBuilder()
                .AddText($"GithubGet: {title}")
                .AddText(subtitle)
                .Show();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
