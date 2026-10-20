using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Tools.Tests;

internal sealed class GitPushProcessProbe : ITaskProcessLauncher
{
    public StubTaskProcess Push { get; } = new();
    public List<TaskProcessStartRequest> Requests { get; } = [];
    public string Configuration { get; set; } = "remote.origin.url\nhttps://user:private-probe@example.invalid/project\0";

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken token)
    {
        Requests.Add(request);
        if (request.Arguments.Contains("push")) return Push;
        var content = request.Arguments.Contains("rev-parse") ? new string('a', 40) + "\n"
            : request.Arguments.Contains("symbolic-ref") ? "refs/heads/fixture\n"
            : request.Arguments.Contains("config") ? Configuration
            : request.Arguments.Contains("get-url") ? "https://user:private-probe@example.invalid/project\n" : "origin\n";
        return new StubTaskProcess { StandardOutput = new StringReader(content) };
    }
}
