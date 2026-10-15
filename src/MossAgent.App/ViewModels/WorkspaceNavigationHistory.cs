namespace MossAgent.App.ViewModels;

public sealed class WorkspaceNavigationHistory
{
    private const int MaximumEntries = 100;
    private readonly Stack<(Guid? ProjectId, Guid? TaskId)> _back = [];
    private readonly Stack<(Guid? ProjectId, Guid? TaskId)> _forward = [];

    public bool CanGoBack => _back.Count > 0;
    public bool CanGoForward => _forward.Count > 0;

    public void Navigate(
        (Guid? ProjectId, Guid? TaskId) current,
        (Guid? ProjectId, Guid? TaskId) next)
    {
        if (current == next) return;
        Push(_back, current);
        _forward.Clear();
    }

    public bool TryGoBack(
        (Guid? ProjectId, Guid? TaskId) current,
        out (Guid? ProjectId, Guid? TaskId) target)
    {
        if (!_back.TryPop(out target)) return false;
        Push(_forward, current);
        return true;
    }

    public bool TryGoForward(
        (Guid? ProjectId, Guid? TaskId) current,
        out (Guid? ProjectId, Guid? TaskId) target)
    {
        if (!_forward.TryPop(out target)) return false;
        Push(_back, current);
        return true;
    }

    public void RemoveTask(Guid taskId)
    {
        RemoveTask(_back, taskId);
        RemoveTask(_forward, taskId);
    }

    private static void Push(Stack<(Guid? ProjectId, Guid? TaskId)> stack,
        (Guid? ProjectId, Guid? TaskId) entry)
    {
        if (stack.TryPeek(out var latest) && latest == entry) return;
        if (stack.Count >= MaximumEntries)
        {
            var retained = stack.Take(MaximumEntries - 1).Reverse().ToArray();
            stack.Clear();
            foreach (var item in retained) stack.Push(item);
        }
        stack.Push(entry);
    }

    private static void RemoveTask(Stack<(Guid? ProjectId, Guid? TaskId)> stack, Guid taskId)
    {
        var retained = stack.Where(entry => entry.TaskId != taskId).Reverse().ToArray();
        stack.Clear();
        foreach (var entry in retained) stack.Push(entry);
    }
}
