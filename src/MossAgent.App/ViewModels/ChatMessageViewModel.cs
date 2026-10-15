using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class ChatMessageViewModel : ObservableObject
{
    private string _content;
    private bool _isToolExpanded;
    private bool _isToolRunning;
    private ToolConversationPayload? _tool;
    private ToolExecutionPhase? _toolPhase;
    private IReadOnlyList<ToolDialogueMessage>? _presentedDialogue;
    private IReadOnlyList<SubagentDialogueEntryViewModel>? _dialogueEntries;
    private DateTimeOffset? _createdAt;

    public ChatMessageViewModel(
        string role,
        string content,
        ToolConversationPayload? tool = null,
        IEnumerable<ChatMessageAttachmentViewModel>? attachments = null,
        string? toolCallId = null,
        bool isToolRunning = false,
        DateTimeOffset? createdAt = null)
    {
        Role = role;
        _content = content;
        _tool = tool;
        _isToolRunning = isToolRunning;
        _createdAt = createdAt;
        ToolCallId = toolCallId;
        _isToolExpanded = tool?.HasDialogue == true;
        if (attachments is not null) Attachments.ReplaceWith(attachments);
        if (IsAssistantMessage) Blocks.ReplaceWith(ChatContentParser.Parse(content));
        ToggleToolCommand = new RelayCommand(
            () => IsToolExpanded = !IsToolExpanded,
            () => HasToolDetails);
    }

    public string Role { get; }
    public DateTimeOffset? CreatedAt => _createdAt;
    internal void SetCreatedAt(DateTimeOffset value) => SetProperty(ref _createdAt, value, nameof(CreatedAt));
    public string? ToolCallId { get; }
    public ToolConversationPayload? Tool
    {
        get => _tool;
        private set
        {
            var dialogueChanged = !ReferenceEquals(_tool?.Dialogue, value?.Dialogue);
            if (!SetProperty(ref _tool, value)) return;
            if (dialogueChanged) OnPropertyChanged(nameof(DialogueEntries));
            OnPropertyChanged(nameof(IsTool));
            OnPropertyChanged(nameof(IsSubagentTool));
            OnPropertyChanged(nameof(HasToolDetails));
            OnPropertyChanged(nameof(ToolSummary));
            OnPropertyChanged(nameof(HasToolSummaryDetails));
            OnPropertyChanged(nameof(ToolSummaryDetails));
            OnPropertyChanged(nameof(ToolStatus));
            if (!HasToolDetails) IsToolExpanded = false;
            ToggleToolCommand.NotifyCanExecuteChanged();
        }
    }
    public bool IsToolRunning
    {
        get => _isToolRunning;
        private set
        {
            if (SetProperty(ref _isToolRunning, value))
                OnPropertyChanged(nameof(ToolStatus));
        }
    }
    public ToolExecutionPhase? ToolPhase
    {
        get => _toolPhase;
        private set
        {
            if (SetProperty(ref _toolPhase, value)) OnPropertyChanged(nameof(ToolStatus));
        }
    }
    public ObservableCollection<ChatMessageAttachmentViewModel> Attachments { get; } = [];
    public ObservableCollection<object> Blocks { get; } = [];
    public IReadOnlyList<SubagentDialogueEntryViewModel> DialogueEntries
    {
        get
        {
            var dialogue = Tool?.Dialogue;
            if (_dialogueEntries is null || !ReferenceEquals(_presentedDialogue, dialogue))
            {
                _dialogueEntries = SubagentDialogueEntryViewModel.Project(dialogue, _dialogueEntries);
                _presentedDialogue = dialogue;
            }
            return _dialogueEntries;
        }
    }
    public IRelayCommand ToggleToolCommand { get; }
    public bool IsTool => Tool is not null;
    public bool IsSubagentTool => Tool?.ToolName == "subagent.run";
    public bool HasAttachments => Attachments.Count > 0;
    public bool HasContent => !string.IsNullOrWhiteSpace(Content);

    /// <summary>
    /// 用户发言显示为靠右的独立消息气泡；工具与其他角色不适用此布局。
    /// </summary>
    public bool IsUserMessage => !IsTool && Role == "你";

    /// <summary>
    /// 非用户的普通消息继续使用分块渲染，以保留代码和 Markdown 展示能力。
    /// </summary>
    public bool IsAssistantMessage => !IsTool && !IsUserMessage;

    public bool IsRegularMessage => !IsTool;
    public bool HasToolDetails => HasToolSummaryDetails
        || !string.IsNullOrWhiteSpace(Tool?.Details) || Tool?.HasDialogue == true;
    public bool HasToolSummaryDetails => (Tool?.Summary ?? string.Empty).AsSpan().TrimEnd().IndexOfAny('\r', '\n') >= 0;
    public string ToolSummaryDetails => HasToolSummaryDetails ? Tool!.Summary : string.Empty;
    public string ToolSummary
    {
        get
        {
            var summary = Tool?.Summary ?? string.Empty;
            var lineEnd = summary.AsSpan().IndexOfAny('\r', '\n');
            return lineEnd < 0 ? summary : summary[..lineEnd];
        }
    }
    public string ToolStatus => IsToolRunning && ToolPhase is { } phase ? WorkspaceToolProgressText.Status(phase)
        : WorkspaceText.Get(IsToolRunning ? "ToolRunning"
        : Tool?.IsSuccess == true ? "ToolCompleted" : Tool?.ErrorCode switch
        {
            "tool_cancelled" => "ToolCancelled",
            "tool_timeout" => "ToolTimedOut",
            _ => "ToolFailed"
        });
    public bool IsToolExpanded
    {
        get => _isToolExpanded;
        set
        {
            SetProperty(ref _isToolExpanded, value);
        }
    }
    public string Content
    {
        get => _content;
        set
        {
            if (!SetProperty(ref _content, value)) return;
            OnPropertyChanged(nameof(HasContent));
            if (IsAssistantMessage) Blocks.ReplaceWith(ChatContentParser.Parse(value));
        }
    }

    public void Append(string value) => Content += value;

    public void UpdateTool(ToolConversationPayload tool, bool isRunning)
    {
        Tool = tool;
        IsToolRunning = isRunning;
        if (!isRunning) ToolPhase = null;
    }

    internal void UpdateToolPhase(ToolExecutionPhase phase)
    {
        if (Enum.IsDefined(phase)) ToolPhase = phase;
    }

    public static ChatMessageViewModel CreateTool(
        ToolConversationPayload tool,
        string? callId = null,
        bool isRunning = false,
        DateTimeOffset? createdAt = null) =>
        new("工具", tool.Summary, tool, toolCallId: callId,
            isToolRunning: isRunning, createdAt: createdAt);
}
