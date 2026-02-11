using System.Text.RegularExpressions;
using GithubGet.Core.Models;

namespace GithubGet.Core.Services;

public sealed class AssetSelector
{
    public SelectedAsset? Select(ReleaseInfo release, AssetRuleSet rules, string? architecture = null)
    {
        if (release.Assets.Count == 0)
        {
            return null;
        }

        var arch = string.IsNullOrWhiteSpace(architecture)
            ? System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()
            : architecture.ToLowerInvariant();

        SelectedAsset? best = null;
        foreach (var asset in release.Assets)
        {
            if (!MatchesInclude(asset.Name, rules))
            {
                continue;
            }

            if (MatchesExclude(asset.Name, rules))
            {
                continue;
            }

            var score = 0;
            score += ExtensionScore(asset.Name, rules.PreferExtensions);
            score += ArchScore(asset.Name, rules.PreferArch, arch);
            score += KeywordScore(asset.Name, rules.PreferKeywords);
            var installKind = DetermineInstallKind(asset.Name);
            var candidate = new SelectedAsset(asset, installKind, score);
            if (best is null || candidate.Score > best.Score)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static bool MatchesInclude(string name, AssetRuleSet rules)
    {
        if (rules.Include.Count == 0)
        {
            return true;
        }

        return rules.Include.Any(pattern => IsMatch(name, pattern));
    }

    private static bool MatchesExclude(string name, AssetRuleSet rules)
    {
        return rules.Exclude.Any(pattern => IsMatch(name, pattern));
    }

    private static bool IsMatch(string text, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int ExtensionScore(string name, List<string> preferExtensions)
    {
        if (preferExtensions.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < preferExtensions.Count; i++)
        {
            var extension = preferExtensions[i];
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return (preferExtensions.Count - i) * 100;
            }
        }

        return 0;
    }

    private static int ArchScore(string name, List<string> preferArch, string currentArch)
    {
        if (preferArch.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < preferArch.Count; i++)
        {
            var arch = preferArch[i].ToLowerInvariant();
            if (name.Contains(arch, StringComparison.OrdinalIgnoreCase))
            {
                return (preferArch.Count - i) * 50;
            }
        }

        if (name.Contains(currentArch, StringComparison.OrdinalIgnoreCase))
        {
            return 25;
        }

        return 0;
    }

    private static int KeywordScore(string name, List<string> preferKeywords)
    {
        var score = 0;
        foreach (var keyword in preferKeywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }
        }

        return score;
    }

    private static InstallKind DetermineInstallKind(string name)
    {
        if (name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
        {
            return InstallKind.Msix;
        }

        if (name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return InstallKind.Msi;
        }

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return InstallKind.Exe;
        }

        return InstallKind.Auto;
    }
}
