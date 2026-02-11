using GithubGet.Core.Models;

namespace GithubGet.Core.Installers;

public interface IScriptRunner
{
    Task<ScriptResult> RunAsync(ScriptExecutionRequest request, CancellationToken ct = default);
}
