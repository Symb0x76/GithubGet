using System.Diagnostics;
using System.Text;
using GithubGet.Core.Models;

namespace GithubGet.Core.Installers;

public sealed class PowerShellScriptRunner : IScriptRunner
{
    public async Task<ScriptResult> RunAsync(ScriptExecutionRequest request, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(request.ScriptPath))
        {
            return new ScriptResult
            {
                Succeeded = false,
                StartedAtUtc = startedAt,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Message = "Script path is empty."
            };
        }

        if (!File.Exists(request.ScriptPath))
        {
            return new ScriptResult
            {
                Succeeded = false,
                StartedAtUtc = startedAt,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Message = $"Script not found: {request.ScriptPath}"
            };
        }

        var command = BuildPowerShellCommand(request);
        var encodedCommand = EncodeCommand(command);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            UseShellExecute = request.RequireAdmin,
            RedirectStandardOutput = !request.RequireAdmin,
            RedirectStandardError = !request.RequireAdmin,
            CreateNoWindow = true
        };

        if (request.RequireAdmin)
        {
            startInfo.Verb = "runas";
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new ScriptResult
                {
                    Succeeded = false,
                    StartedAtUtc = startedAt,
                    EndedAtUtc = DateTimeOffset.UtcNow,
                    Message = "Failed to start PowerShell process."
                };
            }

            Task<string>? stdoutTask = null;
            Task<string>? stderrTask = null;

            if (startInfo.RedirectStandardOutput)
            {
                stdoutTask = process.StandardOutput.ReadToEndAsync();
            }

            if (startInfo.RedirectStandardError)
            {
                stderrTask = process.StandardError.ReadToEndAsync();
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return new ScriptResult
                {
                    Succeeded = false,
                    StartedAtUtc = startedAt,
                    EndedAtUtc = DateTimeOffset.UtcNow,
                    Message = "Script timed out."
                };
            }

            var stdout = stdoutTask is null ? null : await stdoutTask;
            var stderr = stderrTask is null ? null : await stderrTask;
            var exitCode = process.ExitCode;
            var endedAt = DateTimeOffset.UtcNow;

            return new ScriptResult
            {
                Succeeded = exitCode == 0,
                ExitCode = exitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                StartedAtUtc = startedAt,
                EndedAtUtc = endedAt,
                Message = request.RequireAdmin
                    ? "Script executed in elevated mode. Output is unavailable."
                    : null
            };
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ScriptResult
            {
                Succeeded = false,
                StartedAtUtc = startedAt,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Message = $"Script launch failed: {ex.Message}"
            };
        }
    }

    private static string BuildPowerShellCommand(ScriptExecutionRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("$ErrorActionPreference='Stop';");
        SetEnvironmentVariable(builder, "GITHUBGET_DOWNLOAD_PATH", request.Context.DownloadedFilePath);
        SetEnvironmentVariable(builder, "GITHUBGET_DOWNLOAD_DIR", request.Context.DownloadDirectory);
        SetEnvironmentVariable(builder, "GITHUBGET_RELEASE_TAG", request.Context.ReleaseTag);
        SetEnvironmentVariable(builder, "GITHUBGET_RELEASE_TITLE", request.Context.ReleaseTitle);
        SetEnvironmentVariable(builder, "GITHUBGET_RELEASE_URL", request.Context.ReleaseUrl);
        SetEnvironmentVariable(builder, "GITHUBGET_SUBSCRIPTION_ID", request.Context.SubscriptionId);
        SetEnvironmentVariable(builder, "GITHUBGET_SUBSCRIPTION_NAME", request.Context.SubscriptionName);
        builder.Append("& '")
            .Append(EscapePowerShellSingleQuoted(request.ScriptPath))
            .Append("'");
        if (!string.IsNullOrWhiteSpace(request.Args))
        {
            builder.Append(' ').Append(request.Args);
        }

        builder.Append("; if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE } else { exit 0 }");
        return builder.ToString();
    }

    private static void SetEnvironmentVariable(StringBuilder builder, string key, string? value)
    {
        builder.Append("$env:")
            .Append(key)
            .Append("='")
            .Append(EscapePowerShellSingleQuoted(value))
            .Append("';");
    }

    private static string EscapePowerShellSingleQuoted(string? text)
    {
        return (text ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
    }

    private static string EncodeCommand(string command)
    {
        var bytes = Encoding.Unicode.GetBytes(command);
        return Convert.ToBase64String(bytes);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(true);
        }
        catch
        {
        }
    }
}
