using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class TestWorkspaceRepository : IWorkspaceRepository
{
    public ProjectProfile? SavedProject { get; private set; }
    public AgentTask? SavedTask { get; private set; }
    public List<ConversationMessage> Messages { get; } = [];
    public IReadOnlyList<ConversationMessage>? HistoryOverride { get; set; }
    public List<(Guid TaskId, bool IsArchived)> ArchiveOperations { get; } = [];
    public List<(Guid TaskId, bool IsPinned)> PinOperations { get; } = [];
    public IReadOnlyList<AgentTask> ArchivedTasks { get; set; } = [];
    public IReadOnlyList<AgentTask> PinnedTasks { get; set; } = [];
    public IReadOnlyList<ProjectProfile> ProjectItems { get; set; } = [];
    public Dictionary<Guid, IReadOnlyList<AgentTask>> TasksByProject { get; } = [];
    public IReadOnlyList<AgentTask> ProjectlessTasks { get; set; } = [];
    public Func<CancellationToken, Task<IReadOnlyList<AgentTask>>>? GetProjectlessTasksHandler { get; set; }
    public bool RejectPinChange { get; set; }
    public Func<Guid, bool, CancellationToken, Task<bool>>? PinHandler { get; set; }
    public Func<AgentTask, CancellationToken, Task>? SaveTaskHandler { get; set; }
    public Func<AgentTask, string, CancellationToken, Task<AgentTask?>>? RenameTaskHandler { get; set; }
    public Func<Guid, bool, CancellationToken, Task>? ArchiveHandler { get; set; }
    public Func<ProjectProfile, CancellationToken, Task>? SaveProjectHandler { get; set; }
    public Func<Guid, CancellationToken, Task<IReadOnlyList<AgentTask>>>? GetTasksHandler { get; set; }
    public Func<ConversationMessage, CancellationToken, Task>? AppendMessageHandler { get; set; }

    public async Task SaveTaskWithMessagesAsync(AgentTask task, IReadOnlyList<ConversationMessage> messages, CancellationToken cancellationToken)
    {
        var previousTask = SavedTask;
        var previousMessages = Messages.ToArray();
        try
        {
            await SaveTaskAsync(task, cancellationToken);
            foreach (var message in messages) await AppendMessageAsync(message, cancellationToken);
        }
        catch
        {
            SavedTask = previousTask;
            Messages.Clear();
            Messages.AddRange(previousMessages);
            throw;
        }
    }

    public Task<IReadOnlyList<ProjectProfile>> GetProjectsAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(ProjectItems);

    public async Task SaveProjectAsync(ProjectProfile project, CancellationToken cancellationToken)
    {
        if (SaveProjectHandler is not null) await SaveProjectHandler(project, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SavedProject = project;
    }

    public Task<IReadOnlyList<AgentTask>> GetTasksAsync(
        Guid? projectId,
        CancellationToken cancellationToken) =>
        projectId is { } project
            ? GetTasksHandler?.Invoke(project, cancellationToken)
              ?? Task.FromResult(TasksByProject.GetValueOrDefault(project) ?? [])
            : GetProjectlessTasksHandler?.Invoke(cancellationToken) ?? Task.FromResult(ProjectlessTasks);

    public Task<IReadOnlyList<AgentTask>> GetArchivedTasksAsync(
        Guid? projectId,
        CancellationToken cancellationToken) =>
        Task.FromResult(ArchivedTasks);

    public Task<IReadOnlyList<AgentTask>> GetPinnedTasksAsync(CancellationToken cancellationToken) =>
        Task.FromResult(PinnedTasks);

    public Task SaveTaskAsync(AgentTask task, CancellationToken cancellationToken)
    {
        SavedTask = task;
        return SaveTaskHandler?.Invoke(task, cancellationToken) ?? Task.CompletedTask;
    }

    public async Task<AgentTask?> RenameTaskAsync(AgentTask expectedTask, string title, CancellationToken cancellationToken)
    {
        if (RenameTaskHandler is not null) return await RenameTaskHandler(expectedTask, title, cancellationToken);
        var updated = expectedTask with { Title = title.Trim(), UpdatedAt = DateTimeOffset.UtcNow };
        await SaveTaskAsync(updated, cancellationToken);
        return updated;
    }

    public Task<bool> SetTaskPinnedAsync(Guid taskId, bool isPinned, CancellationToken cancellationToken)
    {
        PinOperations.Add((taskId, isPinned));
        return PinHandler?.Invoke(taskId, isPinned, cancellationToken)
            ?? Task.FromResult(!RejectPinChange);
    }

    public Task SetTaskArchivedAsync(
        Guid taskId,
        bool isArchived,
        CancellationToken cancellationToken)
    {
        ArchiveOperations.Add((taskId, isArchived));
        return ArchiveHandler?.Invoke(taskId, isArchived, cancellationToken) ?? Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        Guid taskId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConversationMessage>>(HistoryOverride ?? Messages.Where(message => message.TaskId == taskId).ToArray());

    public Task AppendMessageAsync(
        ConversationMessage message,
        CancellationToken cancellationToken)
    {
        if (AppendMessageHandler is not null) return AppendMessageHandler(message, cancellationToken);
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
