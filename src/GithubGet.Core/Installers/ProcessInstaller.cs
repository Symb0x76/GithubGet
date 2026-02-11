using System.Diagnostics;
using GithubGet.Core.Models;

namespace GithubGet.Core.Installers;

public sealed class ProcessInstaller : IInstaller
{
    public async Task<InstallResult> InstallAsync(InstallRequest request, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        if (request.Kind is InstallKind.None or InstallKind.Msix)
        {
            return new InstallResult
            {
                Succeeded = false,
                StartedAtUtc = startedAt,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Message = "Unsupported install kind."
            };
        }

        var fileName = request.FilePath;
        var arguments = request.Args ?? string.Empty;

        if (request.Kind == InstallKind.Msi)
        {
            fileName = "msiexec.exe";
            arguments = $"{(string.IsNullOrWhiteSpace(request.Args) ? "/i" : request.Args)} \"{request.FilePath}\"";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = request.RequireAdmin,
            RedirectStandardOutput = !request.RequireAdmin,
            RedirectStandardError = !request.RequireAdmin,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return new InstallResult
            {
                Succeeded = false,
                StartedAtUtc = startedAt,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Message = "Failed to start installer."
            };
        }

        Task<string>? outputTask = null;
        Task<string>? errorTask = null;

        if (startInfo.RedirectStandardOutput)
        {
            outputTask = process.StandardOutput.ReadToEndAsync();
        }

        if (startInfo.RedirectStandardError)
        {
            errorTask = process.StandardError.ReadToEndAsync();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }

            return new InstallResult
            {
                Succeeded = false,
                StartedAtUtc = startedAt,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Message = "Installer timed out."
            };
        }

        var output = outputTask is null ? null : await outputTask;
        var error = errorTask is null ? null : await errorTask;
        var endedAt = DateTimeOffset.UtcNow;
        var exitCode = process.ExitCode;

        return new InstallResult
        {
            Succeeded = exitCode == 0,
            ExitCode = exitCode,
            StandardOutput = output,
            StandardError = error,
            StartedAtUtc = startedAt,
            EndedAtUtc = endedAt
        };
    }
}
