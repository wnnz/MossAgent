using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Services;

public sealed record WorkspaceFileUndoPlan(AgentTask Task, ProjectProfile Project,
    ToolExecutionContext Context, IReadOnlyList<ToolFileChange> Changes);
