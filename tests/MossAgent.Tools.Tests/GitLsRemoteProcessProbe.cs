using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Tools.Tests;

internal sealed class GitLsRemoteProcessProbe : ITaskProcessLauncher
{
    public List<StubTaskProcess> LsRemoteProcesses { get; } = [];
    public List<TaskProcessStartRequest> Requests { get; } = [];
    public string Configuration { get; set; } = "remote.origin.url\nhttps://user:private-probe@example.invalid/project\0";
    public string RemoteUrl { get; set; } = "https://user:private-probe@example.invalid/project";
    public string LsRemoteOutput { get; set; } = string.Empty;
    public string LsRemoteError { get; set; } = string.Empty;
    public int LsRemoteExitCode { get; set; }
    public Func<CancellationToken, Task> LsRemoteWait { get; set; } = _ => Task.CompletedTask;
    public Func<Task> LsRemoteTerminate { get; set; } = () => Task.CompletedTask;

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken token)
    {
        Requests.Add(request);
        if (request.Arguments.Contains("ls-remote"))
        {
            var process = new StubTaskProcess
            {
                StandardOutput = new StringReader(LsRemoteOutput),
                StandardError = new StringReader(LsRemoteError),
                ExitCode = LsRemoteExitCode,
                Wait = LsRemoteWait,
                Terminate = LsRemoteTerminate
            };
            LsRemoteProcesses.Add(process);
            return process;
        }

        var content = request.Arguments.Contains("symbolic-ref") ? "refs/heads/fixture\n"
            : request.Arguments.Contains("config") ? Configuration
            : request.Arguments.Contains("get-url") ? RemoteUrl + "\n"
            : "origin\n";
        return new StubTaskProcess { StandardOutput = new StringReader(content) };
    }
}
