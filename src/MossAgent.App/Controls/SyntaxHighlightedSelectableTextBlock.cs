using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.VisualTree;
using MossAgent.App.ViewModels;
using AvaloniaApplication = Avalonia.Application;

namespace MossAgent.App.Controls;

public sealed class SyntaxHighlightedSelectableTextBlock : AppSelectableText
{
    private AvaloniaApplication? _application;

    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<SyntaxHighlightedSelectableTextBlock, string>(nameof(Code), string.Empty);

    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<SyntaxHighlightedSelectableTextBlock, string>(nameof(Language), "text");

    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public string Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CodeProperty
            || change.Property == LanguageProperty)
        {
            RenderCode();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _application = AvaloniaApplication.Current;
        if (_application is not null)
        {
            _application.PropertyChanged += HandleApplicationPropertyChanged;
        }
        RenderCode();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_application is not null)
        {
            _application.PropertyChanged -= HandleApplicationPropertyChanged;
            _application = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void RenderCode()
    {
        var inlines = Inlines ?? new InlineCollection();
        Inlines = inlines;
        inlines.Clear();
        foreach (var token in CodeSyntaxHighlighter.Tokenize(Language, Code))
        {
            var run = new Run(token.Text);
            if (token.ResourceKey.Length > 0
                && _application?.TryGetResource(
                    token.ResourceKey, _application.ActualThemeVariant, out var resource) == true
                && resource is IBrush brush)
            {
                run.Foreground = brush;
            }

            inlines.Add(run);
        }
    }

    private void HandleApplicationPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == AvaloniaApplication.ActualThemeVariantProperty)
        {
            RenderCode();
        }
    }
}
