using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed class FilePreviewCopyCommands : ObservableObject
{
    private readonly Func<bool> _available;
    private readonly Func<string, Task> _copy;
    private FilePreviewPaneViewModel? _pane;
    private FilePreviewTabViewModel? _tab;
    private int _generation;
    private bool _busy;
    private string _notice = string.Empty;

    public FilePreviewCopyCommands(Func<bool> available, Func<string, Task> copy)
    {
        _available = available;
        _copy = copy;
        CopyDefaultCommand = Create("default");
        CopyPathCommand = Create("path");
        CopyRelativePathCommand = Create("relative");
        CopyContentCommand = Create("content");
    }

    public IAsyncRelayCommand CopyDefaultCommand { get; }
    public IAsyncRelayCommand CopyPathCommand { get; }
    public IAsyncRelayCommand CopyRelativePathCommand { get; }
    public IAsyncRelayCommand CopyContentCommand { get; }
    public string DefaultLabel => InspectorText.Get(_tab?.IsImage == true ? "PreviewCopyPath" : "PreviewCopyContent");
    public bool CanShowMenu => IsReady && !_busy;
    public string Notice
    {
        get => _notice;
        private set { if (SetProperty(ref _notice, value)) OnPropertyChanged(nameof(HasNotice)); }
    }
    public bool HasNotice => !string.IsNullOrEmpty(Notice);
    private bool IsReady => _available() && _pane is { HasTask: true, IsOpen: true }
        && _tab is { IsReady: true, IsLoading: false } && _pane.Tabs.Contains(_tab);

    public void Attach(FilePreviewPaneViewModel? pane)
    {
        if (_pane is not null) _pane.PropertyChanged -= HandlePaneChanged;
        _pane = pane;
        if (pane is not null) pane.PropertyChanged += HandlePaneChanged;
        ObserveTab();
    }

    private void HandlePaneChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FilePreviewPaneViewModel.Active)) ObserveTab();
        else if (args.PropertyName == nameof(FilePreviewPaneViewModel.IsOpen)) Invalidate();
    }

    private void ObserveTab()
    {
        if (_tab is not null) _tab.PropertyChanged -= HandleTabChanged;
        _tab = _pane?.Active;
        if (_tab is not null) _tab.PropertyChanged += HandleTabChanged;
        Invalidate();
    }

    private void HandleTabChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(FilePreviewTabViewModel.IsReady) or nameof(FilePreviewTabViewModel.IsLoading)
            or nameof(FilePreviewTabViewModel.Text) or nameof(FilePreviewTabViewModel.IsImage)) Invalidate();
    }

    private void Invalidate() { _generation++; Notice = string.Empty; NotifyCommands(); }
    public void RefreshAvailability() => Invalidate();
    private IAsyncRelayCommand Create(string kind) => new AsyncRelayCommand(token => CopyAsync(kind, token), () => CanCopy(kind));
    private bool CanCopy(string kind) => IsReady && !_busy && (kind != "content" || !_tab!.IsImage)
        && (kind != "relative" || _tab!.RelativePath is not null);

    private async Task CopyAsync(string kind, CancellationToken cancellationToken)
    {
        if (!CanCopy(kind)) return;
        var tab = _tab!;
        var generation = _generation;
        var payload = kind == "path" || kind == "default" && tab.IsImage ? tab.Path
            : kind == "relative" ? tab.RelativePath! : tab.Text;
        _busy = true;
        Notice = string.Empty;
        NotifyCommands();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _copy(payload);
            if (generation == _generation && ReferenceEquals(tab, _tab))
                Notice = InspectorText.Get(cancellationToken.IsCancellationRequested ? "PreviewCopyCancelled" : "PreviewCopied");
        }
        catch (OperationCanceledException)
        { if (generation == _generation) Notice = InspectorText.Get("PreviewCopyCancelled"); }
        catch (Exception)
        { if (generation == _generation) Notice = InspectorText.Get("PreviewCopyFailed"); }
        finally { _busy = false; NotifyCommands(); }
    }

    private void NotifyCommands()
    {
        CopyDefaultCommand.NotifyCanExecuteChanged();
        CopyPathCommand.NotifyCanExecuteChanged();
        CopyRelativePathCommand.NotifyCanExecuteChanged();
        CopyContentCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(DefaultLabel));
        OnPropertyChanged(nameof(CanShowMenu));
    }
}
