namespace GithubGet.Core.Storage;

public static class StorePaths
{
    public static string AppDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GithubGet");

    public static string DatabasePath => Path.Combine(AppDataRoot, "githubget.db");
    public static string CacheRoot => Path.Combine(AppDataRoot, "cache");
    public static string DownloadsRoot => Path.Combine(CacheRoot, "downloads");
    public static string ScriptsRoot => Path.Combine(AppDataRoot, "scripts");
    public static string LogsRoot => Path.Combine(AppDataRoot, "logs");

    public static string GetProjectScriptPath(string owner, string repo)
    {
        var safeOwner = SanitizePathSegment(owner);
        var safeRepo = SanitizePathSegment(repo);
        return Path.Combine(ScriptsRoot, "projects", safeOwner, safeRepo, "install.ps1");
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(DownloadsRoot);
        Directory.CreateDirectory(ScriptsRoot);
        Directory.CreateDirectory(LogsRoot);
    }

    private static string SanitizePathSegment(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(source.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
