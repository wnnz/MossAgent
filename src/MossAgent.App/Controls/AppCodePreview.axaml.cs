using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppCodePreview : AppUserControl
{
    public static readonly StyledProperty<string> CodeProperty = AvaloniaProperty.Register<AppCodePreview, string>(nameof(Code), string.Empty);
    public static readonly StyledProperty<string> FilePathProperty = AvaloniaProperty.Register<AppCodePreview, string>(nameof(FilePath), string.Empty);
    public static readonly StyledProperty<bool> IsDiffProperty = AvaloniaProperty.Register<AppCodePreview, bool>(nameof(IsDiff));
    public static readonly StyledProperty<string> SearchQueryProperty = AvaloniaProperty.Register<AppCodePreview, string>(nameof(SearchQuery), string.Empty);
    public static readonly StyledProperty<int> SelectedLineProperty = AvaloniaProperty.Register<AppCodePreview, int>(nameof(SelectedLine), -1);
    public static readonly StyledProperty<IReadOnlyList<CodePreviewAnnotation>> AnnotationsProperty =
        AvaloniaProperty.Register<AppCodePreview, IReadOnlyList<CodePreviewAnnotation>>(nameof(Annotations), []);
    public static readonly StyledProperty<CodePreviewAnnotationSelection?> AnnotationSelectionProperty =
        AvaloniaProperty.Register<AppCodePreview, CodePreviewAnnotationSelection?>(nameof(AnnotationSelection));
    private readonly TranslateTransform _gutterTransform = new();
    private IReadOnlyList<CodePreviewLine> _rows = [];
    public AppCodePreview() { InitializeComponent(); Classes.Add("app-code-preview"); }
    public string Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public string FilePath { get => GetValue(FilePathProperty); set => SetValue(FilePathProperty, value); }
    public bool IsDiff { get => GetValue(IsDiffProperty); set => SetValue(IsDiffProperty, value); }
    public string SearchQuery { get => GetValue(SearchQueryProperty); set => SetValue(SearchQueryProperty, value); }
    public int SelectedLine { get => GetValue(SelectedLineProperty); set => SetValue(SelectedLineProperty, value); }
    public IReadOnlyList<CodePreviewAnnotation> Annotations { get => GetValue(AnnotationsProperty); set => SetValue(AnnotationsProperty, value); }
    public CodePreviewAnnotationSelection? AnnotationSelection { get => GetValue(AnnotationSelectionProperty); set => SetValue(AnnotationSelectionProperty, value); }
    public ITransform GutterTransform => _gutterTransform;

    private void HandleScrollChanged(object? sender, ScrollChangedEventArgs args) =>
        _gutterTransform.X = Scroller.Offset.X;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (RowsHost is null) return;
        if (change.Property == CodeProperty || change.Property == FilePathProperty || change.Property == IsDiffProperty)
        {
            Classes.Set("diff", IsDiff);
            _rows = CodePreviewParser.Parse(Code ?? string.Empty, FilePath ?? string.Empty, IsDiff);
            RowsHost.ItemsSource = _rows;
            Scroller.Offset = default;
            UpdateSearch();
            UpdateAnnotations();
        }
        else if (change.Property == SearchQueryProperty || change.Property == SelectedLineProperty) UpdateSearch();
        else if (change.Property == AnnotationsProperty || change.Property == AnnotationSelectionProperty) UpdateAnnotations();
    }

    private void UpdateSearch()
    {
        var query = SearchQuery ?? string.Empty;
        query = query[..Math.Min(query.Length, FilePreviewSearchViewModel.MaximumQueryLength)];
        for (var index = 0; index < _rows.Count; index++)
        {
            _rows[index].IsSearchMatch = !IsDiff && query.Length > 0 && !_rows[index].IsHeader
                && _rows[index].Code.Contains(query, StringComparison.OrdinalIgnoreCase);
            _rows[index].IsCurrentSearchMatch = _rows[index].IsSearchMatch && index == SelectedLine;
        }
        Dispatcher.UIThread.Post(BringMatchIntoView, DispatcherPriority.Loaded);
    }

    private void BringMatchIntoView()
    {
        if (!IsEffectivelyVisible || SelectedLine < 0 || SelectedLine >= _rows.Count) return;
        var current = _rows[SelectedLine];
        if (!current.IsCurrentSearchMatch) return;
        var horizontal = Scroller.Offset.X;
        RowsHost.GetVisualDescendants().OfType<AppBorder>()
            .FirstOrDefault(row => row.Classes.Contains("file-code-row") && ReferenceEquals(row.DataContext, current))?.BringIntoView();
        Scroller.Offset = new Vector(horizontal, Scroller.Offset.Y);
    }

    private void UpdateAnnotations()
    {
        foreach (var row in _rows)
        {
            row.Annotations = !IsDiff || row.IsHeader ? [] : (Annotations ?? []).Where(annotation =>
                int.TryParse(annotation.Side == "LEFT" ? row.OldNumber : annotation.Side == "RIGHT" ? row.NewNumber : null, out var line)
                && line == annotation.Line).ToArray();
            row.IsAnnotationTarget = row.Annotations.Any(annotation => annotation.Id == AnnotationSelection?.Id);
        }
        if (AnnotationSelection is not null) Dispatcher.UIThread.Post(BringAnnotationIntoView, DispatcherPriority.Loaded);
    }

    private void BringAnnotationIntoView()
    {
        if (!IsEffectivelyVisible || AnnotationSelection is null) return;
        var row = _rows.FirstOrDefault(item => item.IsAnnotationTarget);
        if (row is null) return;
        var card = RowsHost.GetVisualDescendants().OfType<AppCard>()
            .FirstOrDefault(item => item.DataContext is CodePreviewAnnotation annotation && annotation.Id == AnnotationSelection.Id);
        card?.BringIntoView();
        card?.Focus(NavigationMethod.Tab);
    }
}
