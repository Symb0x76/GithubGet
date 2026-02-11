using GithubGet.Core.Models;
using GithubGet.Core.Services;

namespace GithubGet.Core.Storage;

public interface IGithubGetStore : IApiCache
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Subscription>> GetSubscriptionsAsync(CancellationToken ct = default);
    Task<Subscription?> GetSubscriptionAsync(string id, CancellationToken ct = default);
    Task UpsertSubscriptionAsync(Subscription subscription, CancellationToken ct = default);
    Task DeleteSubscriptionAsync(string id, CancellationToken ct = default);
    Task AddUpdateEventAsync(UpdateEvent updateEvent, CancellationToken ct = default);
    Task UpdateUpdateEventStateAsync(string id, UpdateState state, CancellationToken ct = default);
    Task<IReadOnlyList<UpdateEvent>> GetUpdateEventsAsync(string? subscriptionId = null, int limit = 200, CancellationToken ct = default);
    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);
}
