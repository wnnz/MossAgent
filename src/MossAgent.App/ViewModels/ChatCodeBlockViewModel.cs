namespace MossAgent.App.ViewModels;

public sealed class ChatCodeBlockViewModel(string language, string code)
{
    public string Language { get; } = string.IsNullOrWhiteSpace(language) ? "text" : language;
    public string Code { get; } = code;
}
