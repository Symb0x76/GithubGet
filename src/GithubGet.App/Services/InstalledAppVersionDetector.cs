using System.Text;
using GithubGet.Core.Models;
using Microsoft.Win32;

namespace GithubGet.App.Services;

public static class InstalledAppVersionDetector
{
    private static readonly RegistryHive[] RegistryHives = { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
    private static readonly RegistryView[] RegistryViews = { RegistryView.Registry64, RegistryView.Registry32 };
    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static Dictionary<string, string> ResolveInstalledVersions(IEnumerable<Subscription> subscriptions)
    {
        var apps = ReadInstalledApps();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var subscription in subscriptions)
        {
            var version = ResolveVersion(subscription, apps);
            if (!string.IsNullOrWhiteSpace(version))
            {
                result[subscription.Id] = version;
            }
        }

        return result;
    }

    private static List<InstalledApp> ReadInstalledApps()
    {
        var list = new List<InstalledApp>();
        foreach (var hive in RegistryHives)
        {
            foreach (var view in RegistryViews)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstallKey = baseKey.OpenSubKey(UninstallKeyPath);
                    if (uninstallKey is null)
                    {
                        continue;
                    }

                    foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                    {
                        using var appKey = uninstallKey.OpenSubKey(subKeyName);
                        if (appKey is null)
                        {
                            continue;
                        }

                        var displayName = appKey.GetValue("DisplayName") as string;
                        var displayVersion = appKey.GetValue("DisplayVersion") as string;
                        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(displayVersion))
                        {
                            continue;
                        }

                        list.Add(new InstalledApp(displayName.Trim(), displayVersion.Trim()));
                    }
                }
                catch
                {
                }
            }
        }

        return list
            .GroupBy(app => $"{app.DisplayName}\u001F{app.DisplayVersion}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static string? ResolveVersion(Subscription subscription, IReadOnlyList<InstalledApp> installedApps)
    {
        var candidateNames = BuildCandidateNames(subscription)
            .Select(NormalizeName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (candidateNames.Count == 0)
        {
            return null;
        }

        var bestScore = 0;
        string? bestVersion = null;
        foreach (var app in installedApps)
        {
            var normalizedAppName = NormalizeName(app.DisplayName);
            if (string.IsNullOrWhiteSpace(normalizedAppName))
            {
                continue;
            }

            foreach (var candidate in candidateNames)
            {
                var score = Score(normalizedAppName, candidate);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestVersion = app.DisplayVersion;
            }
        }

        return bestScore >= 70 ? bestVersion : null;
    }

    private static IEnumerable<string> BuildCandidateNames(Subscription subscription)
    {
        yield return subscription.DisplayTitle;
        yield return subscription.Repo;
        yield return subscription.FullName;
        yield return $"{subscription.Owner} {subscription.Repo}";
        yield return subscription.Repo.Replace('.', ' ').Replace('-', ' ').Replace('_', ' ');
    }

    private static string NormalizeName(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWhitespace = false;
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWhitespace = false;
                continue;
            }

            if (char.IsWhiteSpace(character) && !previousWhitespace)
            {
                builder.Append(' ');
                previousWhitespace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static int Score(string appName, string candidate)
    {
        if (string.Equals(appName, candidate, StringComparison.Ordinal))
        {
            return 300;
        }

        if (appName.StartsWith(candidate, StringComparison.Ordinal))
        {
            return 230 - Math.Min(80, appName.Length - candidate.Length);
        }

        if (appName.Contains(candidate, StringComparison.Ordinal))
        {
            return 180 - Math.Min(70, appName.Length - candidate.Length);
        }

        if (candidate.Contains(appName, StringComparison.Ordinal) && appName.Length >= 4)
        {
            return 140;
        }

        var overlap = CommonTokenCount(appName, candidate);
        if (overlap >= 2)
        {
            return 100 + overlap * 10;
        }

        if (overlap == 1 && candidate.Length >= 6)
        {
            return 75;
        }

        return 0;
    }

    private static int CommonTokenCount(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rightTokenSet = right
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        var count = 0;
        foreach (var token in leftTokens)
        {
            if (token.Length < 3)
            {
                continue;
            }

            if (rightTokenSet.Contains(token))
            {
                count++;
            }
        }

        return count;
    }

    private sealed record InstalledApp(string DisplayName, string DisplayVersion);
}
