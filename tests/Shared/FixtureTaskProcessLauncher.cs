using System.Diagnostics;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Testing;

internal sealed class FixtureTaskProcessLauncher : ITaskProcessLauncher
{
    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.RedirectStandardInput,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in request.Arguments) start.ArgumentList.Add(argument);
        if (request.Environment is not null)
            foreach (var entry in request.Environment) start.Environment[entry.Key] = entry.Value;
        if (request.RemovedEnvironmentPrefixes is { } prefixes)
            foreach (var name in start.Environment.Keys.Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray())
                start.Environment.Remove(name);
        return new FixtureTaskProcess(Process.Start(start) ?? throw new InvalidOperationException("Fixture process did not start."));
    }
}
