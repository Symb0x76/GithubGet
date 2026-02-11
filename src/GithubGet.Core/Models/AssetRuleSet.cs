namespace GithubGet.Core.Models;

public sealed record AssetRuleSet
{
    public List<string> Include { get; init; } = new();
    public List<string> Exclude { get; init; } = new();
    public List<string> PreferExtensions { get; init; } = new()
    {
        ".msixbundle",
        ".msix",
        ".msi",
        ".exe"
    };
    public List<string> PreferArch { get; init; } = new()
    {
        "x64",
        "arm64"
    };
    public List<string> PreferKeywords { get; init; } = new();

    public static AssetRuleSet Default => new();
}
