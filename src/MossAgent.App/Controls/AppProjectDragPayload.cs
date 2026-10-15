using Avalonia.Input;

namespace MossAgent.App.Controls;

internal sealed class AppProjectDragPayload(object scope, object source)
{
    public static readonly DataFormat<AppProjectDragPayload> Format =
        DataFormat.CreateInProcessFormat<AppProjectDragPayload>("MossAgent.ProjectNavigation");

    public object Scope { get; } = scope;
    public object Source { get; } = source;
    public bool IsValid { get; private set; } = true;
    public event Action? Invalidated;

    public void Invalidate()
    {
        if (!IsValid) return;
        IsValid = false;
        Invalidated?.Invoke();
    }
}
