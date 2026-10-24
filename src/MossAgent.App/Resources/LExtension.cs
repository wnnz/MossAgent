using Avalonia.Markup.Xaml;

namespace MossAgent.App.Resources;

/// <summary>XAML usage: {res:L 要显示的文本} — resolves through UiText.</summary>
public sealed class LExtension : MarkupExtension
{
    public LExtension(string text) => Text = text;

    public string Text { get; }

    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.T(Text);
}
