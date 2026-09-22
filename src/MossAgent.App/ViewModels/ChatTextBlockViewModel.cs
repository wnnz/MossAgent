namespace MossAgent.App.ViewModels;

public sealed class ChatTextBlockViewModel
{
    public ChatTextBlockViewModel(
        string text,
        bool isHeading = false,
        bool isListItem = false,
        bool isQuote = false,
        string prefix = "")
    {
        Text = text;
        IsHeading = isHeading;
        IsListItem = isListItem;
        IsQuote = isQuote;
        Prefix = prefix;
    }

    public string Text { get; }
    public string Prefix { get; }
    public bool IsHeading { get; }
    public bool IsListItem { get; }
    public bool IsQuote { get; }
    public bool IsBody => !IsHeading && !IsListItem && !IsQuote;
}
