using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Tools.Tests;

internal sealed class GitCommitProcessProbe : ITaskProcessLauncher
{
    public StubTaskProcess Commit { get; } = new();
    public List<TaskProcessStartRequest> Requests { get; } = [];
    public string Index { get; set; } = "100644 " + new string('b', 40) + " 0\tselected.txt\0";

    public ITaskProcess Start(TaskProcessStartRequest request, CancellationToken token)
    {
        Requests.Add(request);
        if (request.Arguments.Contains("commit")) return Commit;
        var content = request.Arguments.Contains("rev-parse") ? new string('a', 40) + "\n"
            : request.Arguments.Contains("symbolic-ref") ? "refs/heads/fixture\n"
            : request.Arguments.Contains("ls-files") ? Index : "selected.txt\0";
        return new StubTaskProcess { StandardOutput = new StringReader(content) };
    }
}
