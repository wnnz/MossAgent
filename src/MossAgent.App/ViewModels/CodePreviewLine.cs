using CommunityToolkit.Mvvm.ComponentModel;

namespace MossAgent.App.ViewModels;

public sealed class CodePreviewLine(string oldNumber, string newNumber, string prefix, string code, string language, string kind) : ObservableObject
{
    private bool _searchMatch;
    private bool _currentSearchMatch;
    private bool _annotationTarget;
    private IReadOnlyList<CodePreviewAnnotation> _annotations = [];
    public string OldNumber { get; } = oldNumber;
    public string NewNumber { get; } = newNumber;
    public string Prefix { get; } = prefix;
    public string Code { get; } = code;
    public string Language { get; } = language;
    public string Kind { get; } = kind;
    public bool IsSearchMatch { get => _searchMatch; internal set => SetProperty(ref _searchMatch, value); }
    public bool IsCurrentSearchMatch { get => _currentSearchMatch; internal set => SetProperty(ref _currentSearchMatch, value); }
    public bool IsAnnotationTarget { get => _annotationTarget; internal set => SetProperty(ref _annotationTarget, value); }
    public IReadOnlyList<CodePreviewAnnotation> Annotations
    {
        get => _annotations;
        internal set { SetProperty(ref _annotations, value); OnPropertyChanged(nameof(HasAnnotations)); }
    }
    public bool HasAnnotations => Annotations.Count > 0;
    public bool IsAdded => Kind == "added";
    public bool IsRemoved => Kind == "removed";
    public bool IsHeader => Kind == "header";
}
