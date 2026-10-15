using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.App.Services;

namespace MossAgent.App.ViewModels;

public sealed class FilePreviewFilePickerViewModel : ObservableObject, IDisposable
{
    private readonly FilePreviewPaneViewModel _owner;
    private readonly IFilePickerService? _picker;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _selection;
    private int _selectionVersion;
    private bool _disposed;
    private string _status = string.Empty;

    public FilePreviewFilePickerViewModel(FilePreviewPaneViewModel owner, IFilePickerService? picker, TimeProvider? timeProvider = null)
    {
        _owner = owner;
        _picker = picker;
        _timeProvider = timeProvider ?? TimeProvider.System;
        AddFilesCommand = new GuardedAsyncRelayCommand(PickAsync, () => CanPick);
        owner.PropertyChanged += HandleOwnerChanged;
        owner.Tabs.CollectionChanged += HandleTabsChanged;
    }

    public GuardedAsyncRelayCommand AddFilesCommand { get; }
    public bool CanPick => !_disposed && _picker is not null && _owner.HasTask && _owner.IsOpen
        && _selection is null && _owner.Tabs.Count < FilePreviewPaneViewModel.MaximumTabs;
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public string Status
    {
        get => _status;
        private set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); }
    }

    private async Task PickAsync(CancellationToken commandToken)
    {
        if (!CanPick) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5), _timeProvider);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(commandToken, timeout.Token);
        _selection = cancellation;
        _selectionVersion = _owner.ContextVersion;
        var version = _selectionVersion;
        bool IsOwnerCurrent() => !_disposed && _owner.HasTask && _owner.IsOpen && _owner.ContextVersion == version;
        bool IsCurrent() => IsOwnerCurrent() && !cancellation.IsCancellationRequested;
        Status = string.Empty;
        NotifyState();
        try
        {
            var paths = await _picker!.PickPreviewFilesAsync(cancellation.Token);
            if (!IsCurrent()) return;
            if (paths.Count > FilePreviewPaneViewModel.MaximumTabs)
            { Status = InspectorText.Get("PreviewTabLimit"); return; }
            foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!IsCurrent()) break;
                await _owner.OpenAsync(path, cancellation.Token);
            }
        }
        catch (OperationCanceledException)
        { if (IsOwnerCurrent()) Status = InspectorText.Get("PreviewPickCancelled"); }
        catch (Exception)
        { if (IsOwnerCurrent()) Status = InspectorText.Get("PreviewPickFailed"); }
        finally
        {
            if (IsOwnerCurrent() && cancellation.IsCancellationRequested) Status = InspectorText.Get("PreviewPickCancelled");
            if (ReferenceEquals(_selection, cancellation)) _selection = null;
            NotifyState();
        }
    }

    private void HandleOwnerChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_selectionVersion != _owner.ContextVersion || !_owner.HasTask || !_owner.IsOpen)
        {
            _selection?.Cancel();
            Status = string.Empty;
        }
        NotifyState();
    }

    private void HandleTabsChanged(object? sender, NotifyCollectionChangedEventArgs args) => NotifyState();
    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanPick));
        AddFilesCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _owner.PropertyChanged -= HandleOwnerChanged;
        _owner.Tabs.CollectionChanged -= HandleTabsChanged;
        _selection?.Cancel();
        AddFilesCommand.Dispose();
        NotifyState();
    }
}
