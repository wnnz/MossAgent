using Avalonia;

namespace MossAgent.App.ViewModels;

public sealed class ChatTextBlockViewModel
{
    public ChatTextBlockViewModel(
        string text,
        bool isHeading = false,
        bool isListItem = false,
        bool isQuote = false,
        string prefix = "",
        int indentLevel = 0)
    {
        Text = text;
        IsHeading = isHeading;
        IsListItem = isListItem;
        IsQuote = isQuote;
        Prefix = prefix;
        IndentLevel = indentLevel;
    }

    public string Text { get; }
    public string Prefix { get; }
    public int IndentLevel { get; }
    public Thickness IndentMargin => new(IndentLevel * 18, 0, 0, 0);
    public bool IsHeading { get; }
    public bool IsListItem { get; }
    public bool IsQuote { get; }
    public bool IsBody => !IsHeading && !IsListItem && !IsQuote;
}
