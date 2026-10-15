using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal sealed class SubagentFileChangeCollector
{
    private readonly List<ToolFileChange> _changes = [];
    public bool IsTruncated { get; private set; }
    public IReadOnlyList<ToolFileChange>? Snapshot() => _changes.Count == 0 ? null : _changes.ToArray();

    public void Add(ToolResult result)
    {
        if (result.FileChanges is not null) _changes.AddRange(result.FileChanges);
        IsTruncated |= result.FileChangesTruncated;
    }

    public ToolResult Apply(ToolResult result) => result with
    {
        FileChanges = Snapshot(),
        FileChangesTruncated = IsTruncated
    };
}
