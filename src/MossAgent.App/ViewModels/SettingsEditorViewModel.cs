using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MossAgent.App.ViewModels;

public abstract class SettingsEditorViewModel(params string[] draftProperties) : ObservableObject
{
    private readonly HashSet<string> _draftProperties = new(draftProperties, StringComparer.Ordinal);
    protected long EditorRevision { get; private set; }
    protected void InvalidateEditor() => EditorRevision++;

    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        if (args.PropertyName is { } property && _draftProperties.Contains(property)) EditorRevision++;
        base.OnPropertyChanged(args);
    }
}
