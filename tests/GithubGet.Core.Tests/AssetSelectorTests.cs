using GithubGet.Core.Models;
using GithubGet.Core.Services;

namespace GithubGet.Core.Tests;

public class AssetSelectorTests
{
    [Fact]
    public void Select_PrefersExtensions()
    {
        var release = new ReleaseInfo(
            1,
            "v1.0.0",
            "Test",
            DateTimeOffset.UtcNow,
            false,
            false,
            "https://example.com",
            null,
            new[]
            {
                new ReleaseAsset("app.exe", 10, "https://example.com/app.exe", "application/octet-stream"),
                new ReleaseAsset("app.msi", 10, "https://example.com/app.msi", "application/octet-stream"),
                new ReleaseAsset("app.msix", 10, "https://example.com/app.msix", "application/octet-stream")
            });

        var selector = new AssetSelector();
        var selected = selector.Select(release, AssetRuleSet.Default);

        Assert.NotNull(selected);
        Assert.Equal("app.msix", selected!.Asset.Name);
        Assert.Equal(InstallKind.Msix, selected.InstallKind);
    }

    [Fact]
    public void Select_RespectsIncludeAndExclude()
    {
        var release = new ReleaseInfo(
            1,
            "v1.0.0",
            "Test",
            DateTimeOffset.UtcNow,
            false,
            false,
            "https://example.com",
            null,
            new[]
            {
                new ReleaseAsset("app-x64.msix", 10, "https://example.com/app-x64.msix", "application/octet-stream"),
                new ReleaseAsset("app-arm64.msix", 10, "https://example.com/app-arm64.msix", "application/octet-stream"),
                new ReleaseAsset("app-portable.zip", 10, "https://example.com/app-portable.zip", "application/zip")
            });

        var rules = new AssetRuleSet
        {
            Include = new List<string> { "msix" },
            Exclude = new List<string> { "arm64" },
            PreferExtensions = new List<string> { ".msix" },
            PreferArch = new List<string> { "x64", "arm64" }
        };

        var selector = new AssetSelector();
        var selected = selector.Select(release, rules);

        Assert.NotNull(selected);
        Assert.Equal("app-x64.msix", selected!.Asset.Name);
    }
}
