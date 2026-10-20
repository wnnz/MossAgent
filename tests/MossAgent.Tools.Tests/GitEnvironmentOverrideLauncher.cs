using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Tools.Tests;

internal sealed class GitEnvironmentOverrideLauncher(string index, string trace) : ITaskProcessLauncher
{
    public List<TaskProcessStartRequest> Requests { get; } = [];

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken token)
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (request.Environment is { } supplied)
            foreach (var entry in supplied) environment[entry.Key] = entry.Value;
        environment["GIT_INDEX_FILE"] = index;
        environment["GIT_TRACE"] = trace;
        request = request with { Environment = environment };
        Requests.Add(request);
        return new FixtureTaskProcessLauncher().Start(request, token);
    }
}
