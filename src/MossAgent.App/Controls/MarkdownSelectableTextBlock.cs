using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace MossAgent.App.Controls;

public sealed class MarkdownSelectableTextBlock : AppSelectableText
{
    private readonly List<MarkdownCodeRun> _codeInlines = [];
    private int _textOffset;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownSelectableTextBlock, string?>(nameof(Markdown));

    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownSelectableTextBlock, FontFamily>(
            nameof(CodeFontFamily), new FontFamily("Consolas, Cascadia Code, Courier New"));
    public static readonly StyledProperty<double> CodeFontSizeProperty =
        AvaloniaProperty.Register<MarkdownSelectableTextBlock, double>(nameof(CodeFontSize), 12);
    public static readonly StyledProperty<IBrush?> CodeBackgroundProperty =
        AvaloniaProperty.Register<MarkdownSelectableTextBlock, IBrush?>(nameof(CodeBackground));
    public static readonly StyledProperty<Thickness> CodeBackgroundPaddingProperty =
        AvaloniaProperty.Register<MarkdownSelectableTextBlock, Thickness>(nameof(CodeBackgroundPadding), new Thickness(0, 2));
    public static readonly StyledProperty<double> CodeCornerRadiusProperty =
        AvaloniaProperty.Register<MarkdownSelectableTextBlock, double>(nameof(CodeCornerRadius), 3,
            validate: value => double.IsFinite(value) && value >= 0);

    static MarkdownSelectableTextBlock() => AffectsRender<MarkdownSelectableTextBlock>(
        CodeBackgroundProperty, CodeBackgroundPaddingProperty, CodeCornerRadiusProperty);

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    public double CodeFontSize { get => GetValue(CodeFontSizeProperty); set => SetValue(CodeFontSizeProperty, value); }
    public IBrush? CodeBackground { get => GetValue(CodeBackgroundProperty); set => SetValue(CodeBackgroundProperty, value); }
    public Thickness CodeBackgroundPadding { get => GetValue(CodeBackgroundPaddingProperty); set => SetValue(CodeBackgroundPaddingProperty, value); }
    public double CodeCornerRadius { get => GetValue(CodeCornerRadiusProperty); set => SetValue(CodeCornerRadiusProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
        {
            RenderMarkdown();
        }
        else if (change.Property == CodeFontFamilyProperty || change.Property == CodeFontSizeProperty)
        {
            foreach (var run in _codeInlines)
            {
                run.Inline.FontFamily = CodeFontFamily;
                run.Inline.FontSize = CodeFontSize;
            }
        }
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (CodeBackground is { } background && _codeInlines.Count > 0)
        {
            var padding = CodeBackgroundPadding;
            var codeHeight = CodeFontSize + padding.Top + padding.Bottom;
            var radius = CodeCornerRadius;
            var layout = TextLayout;
            using var transform = context.PushTransform(Matrix.CreateTranslation(origin));
            foreach (var run in _codeInlines)
            {
                foreach (var bounds in layout.HitTestTextRange(run.Start, run.Inline.Text?.Length ?? 0))
                {
                    var height = Math.Min(bounds.Height, codeHeight);
                    var rectangle = new Rect(bounds.X - padding.Left, bounds.Y + (bounds.Height - height) / 2,
                        bounds.Width + padding.Left + padding.Right, height);
                    context.DrawRectangle(background, null, rectangle, radius, radius);
                }
            }
        }
        base.RenderTextLayout(context, origin);
    }

    private void RenderMarkdown()
    {
        _codeInlines.Clear();
        _textOffset = 0;
        var target = EnsureInlines();
        var markdown = Markdown ?? string.Empty;
        var hasParagraph = false;
        foreach (var paragraph in Markdig.Markdown.Parse(markdown, Pipeline).OfType<ParagraphBlock>())
        {
            if (hasParagraph) target.Add(CreateLineBreak());
            hasParagraph = true;
            if (paragraph.Inline is { } inline) AppendInlines(inline, target);
        }
        if (!hasParagraph) target.Add(CreateTextRun(markdown));
    }

    private InlineCollection EnsureInlines()
    {
        var target = Inlines ?? new InlineCollection();
        Inlines = target;
        target.Clear();
        return target;
    }

    private void AppendInlines(ContainerInline source, InlineCollection target)
    {
        for (var current = source.FirstChild; current is not null; current = current.NextSibling)
        {
            target.Add(CreateInline(current));
        }
    }

    private AvaloniaInline CreateInline(MarkdigInline source)
    {
        return source switch
        {
            LiteralInline literal => CreateTextRun(literal.Content.ToString()),
            CodeInline code => CreateCodeInline(code),
            LineBreakInline => CreateLineBreak(),
            EmphasisInline emphasis => CreateEmphasis(emphasis),
            LinkInline link => CreateLink(link),
            HtmlInline html => CreateTextRun(html.Tag),
            ContainerInline container => CreateContainer(container),
            _ => CreateTextRun(source.ToString())
        };
    }

    private Run CreateCodeInline(CodeInline source)
    {
        var start = _textOffset;
        var run = CreateTextRun(source.Content);
        run.FontFamily = CodeFontFamily;
        run.FontSize = CodeFontSize;
        _codeInlines.Add(new(run, start));
        return run;
    }

    private Run CreateTextRun(string? text)
    {
        _textOffset += text?.Length ?? 0;
        return new Run(text);
    }

    private LineBreak CreateLineBreak()
    {
        _textOffset += Environment.NewLine.Length;
        return new LineBreak();
    }

    private Span CreateEmphasis(EmphasisInline source)
    {
        var span = new Span();
        if (source.DelimiterChar == '~')
        {
            span.TextDecorations = Avalonia.Media.TextDecorations.Strikethrough;
        }
        else
        {
            if (source.DelimiterCount >= 2) span.FontWeight = FontWeight.Bold;
            if (source.DelimiterCount % 2 != 0) span.FontStyle = FontStyle.Italic;
        }
        AppendInlines(source, span.Inlines);
        return span;
    }

    private Span CreateLink(LinkInline source)
    {
        var span = CreateContainer(source);
        span.TextDecorations = Avalonia.Media.TextDecorations.Underline;
        return span;
    }

    private Span CreateContainer(ContainerInline source)
    {
        var span = new Span();
        AppendInlines(source, span.Inlines);
        return span;
    }
}
