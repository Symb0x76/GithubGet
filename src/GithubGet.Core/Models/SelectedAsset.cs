namespace GithubGet.Core.Models;

public sealed record SelectedAsset(
    ReleaseAsset Asset,
    InstallKind InstallKind,
    int Score);
