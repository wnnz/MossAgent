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
    AuditPaneViewModel audit,
    EmbeddedBrowserWorkspaceViewModel embeddedBrowsers,
    FilePreviewPaneViewModel previews)
{
    public FilePreviewPaneViewModel Previews => previews;
    public ToolExecutionContext Attach(AgentTask task, ProjectProfile? project)
    {
        var context = CreateContext(task, project);
        if (project is null)
        {
            ClearSelection();
            audit.Attach(task.Id);
            return context;
        }
        embeddedBrowsers.SelectTask(task.Id, context);
        terminal.AttachTask(task.Id, context.WorkingDirectory, context);
        diff.Attach(context);
        previews.Attach(context);
        audit.Attach(task.Id);
        return context;
    }

    public ToolExecutionContext CreateContext(AgentTask task, ProjectProfile? project)
    {
        if (task.ProjectId != project?.Id || (project is null && task.WorktreePath is not null))
            throw new InvalidOperationException("Task project does not match the selected context.");
        if (project is null)
            return new(task.Id, string.Empty, [], ApprovalPolicy.ReadOnly) { ToolsEnabled = false };
        return new(
            task.Id,
            task.WorktreePath ?? project.PrimaryDirectory,
            taskFactory.GetAuthorizedRoots(project, task),
            task.ApprovalPolicy,
            ArtifactDirectory: artifacts.GetTaskDirectory(task.Id),
            BrowserSession: browserSessions.GetLazySession(task.Id));
    }

    public void ShowDiff(string content) => diff.Show(content);
    public Task ReviewDiffAsync(Guid taskId, CancellationToken cancellationToken) => diff.ReviewAsync(taskId, cancellationToken);

    public void ClearSelection()
    {
        embeddedBrowsers.SelectTask(null);
        terminal.AttachTask(null, null);
        diff.Attach(null);
        previews.Attach(null);
        audit.Attach(null);
    }

    public Task ReleaseBrowserAsync(Guid taskId) => browserSessions.ReleaseAsync(taskId);
}
