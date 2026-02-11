using GithubGet.Core.Models;

namespace GithubGet.Core.Installers;

public interface IInstaller
{
    Task<InstallResult> InstallAsync(InstallRequest request, CancellationToken ct = default);
}
