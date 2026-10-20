using MossAgent.Application.Agent;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed record WorkspaceRunPresentation(
    Func<WorkspaceRunState, AgentEvent, Task> PresentEvent,
    Action<WorkspaceRunState, string> SetActivity,
    Action<AgentTask> ApplyTaskUpdate,
    Action RunStateChanged);
