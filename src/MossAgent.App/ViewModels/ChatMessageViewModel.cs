using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class ChatMessageViewModel : ObservableObject
{
    private string _content;
    private bool _isToolExpanded;

    public ChatMessageViewModel(
        string role,
        string content,
        ToolConversationPayload? tool = null)
    {
        Role = role;
        _content = content;
        Tool = tool;
        Blocks.ReplaceWith(ChatContentParser.Parse(content));
        ToggleToolCommand = new RelayCommand(
            () => IsToolExpanded = !IsToolExpanded,
            () => HasToolDetails);
    }

    public string Role { get; }
    public ToolConversationPayload? Tool { get; }
    public ObservableCollection<object> Blocks { get; } = [];
    public IRelayCommand ToggleToolCommand { get; }
    public bool IsTool => Tool is not null;
    public bool IsRegularMessage => !IsTool;
    public bool HasToolDetails => !string.IsNullOrWhiteSpace(Tool?.Details);
    public string ToolStatus => Tool?.IsSuccess == true ? "完成" : "失败";
    public string ExpandGlyph => IsToolExpanded ? "⌄" : "›";
    public bool IsToolExpanded
    {
        get => _isToolExpanded;
        set
        {
            if (SetProperty(ref _isToolExpanded, value))
                OnPropertyChanged(nameof(ExpandGlyph));
        }
    }
    public string Content
    {
        get => _content;
        set
        {
            if (!SetProperty(ref _content, value)) return;
            Blocks.ReplaceWith(ChatContentParser.Parse(value));
        }
    }

    public void Append(string value) => Content += value;

    public static ChatMessageViewModel CreateTool(ToolConversationPayload tool) =>
        new("工具", tool.Summary, tool);
}
