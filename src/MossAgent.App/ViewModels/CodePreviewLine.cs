using CommunityToolkit.Mvvm.ComponentModel;

namespace MossAgent.App.ViewModels;

public sealed class CodePreviewLine(string oldNumber, string newNumber, string prefix, string code, string language, string kind) : ObservableObject
{
    private bool _searchMatch;
    private bool _currentSearchMatch;
    public string OldNumber { get; } = oldNumber;
    public string NewNumber { get; } = newNumber;
    public string Prefix { get; } = prefix;
    public string Code { get; } = code;
    public string Language { get; } = language;
    public string Kind { get; } = kind;
    public bool IsSearchMatch { get => _searchMatch; internal set => SetProperty(ref _searchMatch, value); }
    public bool IsCurrentSearchMatch { get => _currentSearchMatch; internal set => SetProperty(ref _currentSearchMatch, value); }
    public bool IsAdded => Kind == "added";
    public bool IsRemoved => Kind == "removed";
    public bool IsHeader => Kind == "header";
}
