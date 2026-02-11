using GithubGet.Core.Services;
using GithubGet.Core.Storage;
using GithubGet.Worker;

if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    PrintHelp();
    return 1;
}

var runOnce = args.Contains("--run-once", StringComparer.OrdinalIgnoreCase);
if (!runOnce)
{
    PrintHelp();
    return 1;
}

var subscriptionId = GetValue(args, "--subscription");
var noToast = args.Contains("--no-toast", StringComparer.OrdinalIgnoreCase);

var store = new SqliteGithubGetStore();
using var httpClient = new HttpClient();
var token = Environment.GetEnvironmentVariable("GITHUBGET_TOKEN");
var client = new GitHubClient(httpClient, new GitHubClientOptions { Token = token }, store);
var checker = new UpdateChecker(store, client, new AssetSelector());

var summary = await checker.RunOnceAsync(new UpdateCheckOptions
{
    SubscriptionId = subscriptionId,
    NoToast = noToast
});

var notified = 0;
if (!noToast)
{
    var notifier = new WorkerNotificationService(store);
    notified = await notifier.NotifyPendingUpdatesAsync();
}

Console.WriteLine($"Checked {summary.Checked}, Updated {summary.Updated}, Failed {summary.Failed}");
Console.WriteLine($"Notified {notified}");
return summary.Failed > 0 ? 2 : 0;

static string? GetValue(string[] args, string key)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 < args.Length)
        {
            return args[i + 1];
        }
    }

    return null;
}

static void PrintHelp()
{
    Console.WriteLine("GithubGet.Worker");
    Console.WriteLine("  --run-once           Run a single update check.");
    Console.WriteLine("  --subscription <id>  Only check one subscription.");
    Console.WriteLine("  --no-toast           Skip toast notification.");
}
