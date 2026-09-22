using CommunityToolkit.Mvvm.ComponentModel;

namespace MossAgent.App.ViewModels;

public sealed class ChatMessageViewModel : ObservableObject
{
    private string _content;

    public ChatMessageViewModel(string role, string content)
    {
        Role = role;
        _content = content;
    }

    public string Role { get; }
    public string Content { get => _content; set => SetProperty(ref _content, value); }

    public void Append(string value) => Content += value;
}

