using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class FilePreviewSearchViewModel : ObservableObject
{
    public const int MaximumQueryLength = 256;
    private IReadOnlyList<string> _lines = [];
    private int[] _matches = [];
    private string _query = string.Empty;
    private int _position = -1;
    private bool _available;
    private bool _open;

    public FilePreviewSearchViewModel()
    {
        ShowCommand = new RelayCommand(() => { if (_available) IsOpen = true; }, () => _available);
        CloseCommand = new RelayCommand(() => IsOpen = false);
        NextCommand = new RelayCommand(() => Move(1), () => _available && _matches.Length > 0);
        PreviousCommand = new RelayCommand(() => Move(-1), () => _available && _matches.Length > 0);
    }

    public bool IsOpen { get => _open; private set { if (SetProperty(ref _open, value)) NotifyResults(); } }
    public string Query
    {
        get => _query;
        set
        {
            var bounded = (value ?? string.Empty)[..Math.Min(value?.Length ?? 0, MaximumQueryLength)];
            if (SetProperty(ref _query, bounded)) Find();
        }
    }
    public int MatchCount => _matches.Length;
    public int SelectedLine => IsOpen && _position >= 0 ? _matches[_position] : -1;
    public string ActiveQuery => IsOpen ? Query : string.Empty;
    public string ResultLabel => Query.Length == 0 ? InspectorText.Get("PreviewFindHint")
        : _matches.Length == 0 ? InspectorText.Get("PreviewNoMatches")
        : InspectorText.Format("PreviewMatchLines", _position + 1, _matches.Length);
    public IRelayCommand ShowCommand { get; }
    public IRelayCommand CloseCommand { get; }
    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }

    internal void Reset(string content)
    {
        _lines = CodePreviewParser.ReadLines(content, out _);
        SetProperty(ref _query, string.Empty, nameof(Query));
        Find();
        IsOpen = false;
    }

    internal void SetAvailable(bool available)
    {
        _available = available;
        if (!available) IsOpen = false;
        ShowCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }

    private void Find()
    {
        List<int>? matches = null;
        if (Query.Length > 0)
        {
            for (var index = 0; index < _lines.Count; index++)
                if (_lines[index].Contains(Query, StringComparison.OrdinalIgnoreCase)) (matches ??= []).Add(index);
        }
        _matches = matches?.ToArray() ?? [];
        _position = _matches.Length > 0 ? 0 : -1;
        NotifyResults();
    }

    private void Move(int direction)
    {
        if (!_available || _matches.Length == 0) return;
        _position = (_position + direction + _matches.Length) % _matches.Length;
        NotifyResults();
    }

    private void NotifyResults()
    {
        OnPropertyChanged(nameof(MatchCount));
        OnPropertyChanged(nameof(SelectedLine));
        OnPropertyChanged(nameof(ActiveQuery));
        OnPropertyChanged(nameof(ResultLabel));
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }
}
