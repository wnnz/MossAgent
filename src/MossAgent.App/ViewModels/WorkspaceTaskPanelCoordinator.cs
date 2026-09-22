using MossAgent.Application.Artifacts;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceTaskPanelCoordinator(
    ITaskArtifactPaths artifacts,
    ITaskBrowserSessionManager browserSessions,
    WorkspaceTaskFactory taskFactory,
    TerminalPaneViewModel terminal,
    DiffPaneViewModel diff,
    AuditPaneViewModel audit)
{
    public ToolExecutionContext Attach(AgentTask task, ProjectProfile project)
    {
        var context = CreateContext(task, project);
        terminal.AttachTask(task.Id, context.WorkingDirectory);
        diff.Attach(context);
        audit.Attach(task.Id);
        return context;
    }

    public ToolExecutionContext CreateContext(AgentTask task, ProjectProfile project) =>
        new(
            task.Id,
            task.WorktreePath ?? project.PrimaryDirectory,
            taskFactory.GetAuthorizedRoots(project, task),
            task.ApprovalPolicy,
            ArtifactDirectory: artifacts.GetTaskDirectory(task.Id),
            BrowserSession: browserSessions.GetLazySession(task.Id));

    public void ShowDiff(string content) => diff.Show(content);
}
