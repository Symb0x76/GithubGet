using System.Diagnostics;

namespace GithubGet.App.Services;

public sealed class TaskSchedulerService
{
    public const string TaskName = "GithubGet.UpdateCheck";

    public async Task<string> CreateOrUpdateDailyTaskAsync(string workerExecutablePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workerExecutablePath))
        {
            return "未找到自动检查组件，请重新安装或修复应用。";
        }

        if (!File.Exists(workerExecutablePath))
        {
            return "自动检查组件不存在，请重新安装或修复应用。";
        }

        var escapedAction = $"\\\"{workerExecutablePath}\\\" --run-once";
        var createArgs = $"/Create /TN \"{TaskName}\" /SC DAILY /ST 09:00 /F /TR \"{escapedAction}\"";
        var result = await RunSchtasksAsync(createArgs, ct);
        return result.Success ? "每日自动检查已启用（每天 09:00）" : $"启用失败: {result.Output}";
    }

    public async Task<string> DeleteTaskAsync(CancellationToken ct = default)
    {
        var result = await RunSchtasksAsync($"/Delete /TN \"{TaskName}\" /F", ct);
        return result.Success ? "每日自动检查已关闭" : $"关闭失败: {result.Output}";
    }

    public async Task<string> QueryTaskAsync(CancellationToken ct = default)
    {
        var result = await QueryTaskStateAsync(ct);
        return result.Success ? "每日自动检查已启用" : "每日自动检查未启用";
    }

    public async Task<(bool Success, string Output)> QueryTaskStateAsync(CancellationToken ct = default)
    {
        return await RunSchtasksAsync($"/Query /TN \"{TaskName}\"", ct);
    }

    public static string? FindDefaultWorkerPath()
    {
        var candidates = new List<string>();

        var direct = Path.Combine(AppContext.BaseDirectory, "GithubGet.Worker.exe");
        candidates.Add(direct);

        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            current = Path.GetFullPath(Path.Combine(current, ".."));
            candidates.Add(Path.Combine(current, "src", "GithubGet.Worker", "bin", "Debug", "net8.0-windows10.0.19041.0", "GithubGet.Worker.exe"));
            candidates.Add(Path.Combine(current, "src", "GithubGet.Worker", "bin", "Release", "net8.0-windows10.0.19041.0", "GithubGet.Worker.exe"));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<(bool Success, string Output)> RunSchtasksAsync(string arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var output = $"{await stdout}\n{await stderr}".Trim();
        return (process.ExitCode == 0, output);
    }
}
