using System.Text.Json.Serialization;
using MossAgent.Tools.Abstractions;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

public sealed class SubagentDialogueEntryViewModel(ToolDialogueMessage message)
{
    private IReadOnlyList<object>? _blocks;

    public string Role { get; } = message.Role;
    public string Content { get; } = message.Content;
    [JsonIgnore]
    public IReadOnlyList<object> Blocks => _blocks ??= IsAnswer
        ? ChatContentParser.Parse(Content) : [];
    public bool IsStatus => Role == UiText.T("状态");
    public bool IsTool => Role.StartsWith(UiText.T("工具 · "), StringComparison.Ordinal);
    public bool IsAnswer => Role == UiText.T("子代理");
    public bool IsTask => Role == UiText.T("任务");
    public bool IsOther => !IsStatus && !IsTool && !IsAnswer && !IsTask;
    public bool ShowLabel => !IsStatus;
    public string DisplayRole => IsTool ? Role[UiText.T("工具 · ").Length..] : Role;

    internal static IReadOnlyList<SubagentDialogueEntryViewModel> Project(
        IReadOnlyList<ToolDialogueMessage>? dialogue, IReadOnlyList<SubagentDialogueEntryViewModel>? previous)
    {
        if (dialogue is not { Count: > 0 }) return [];
        var entries = previous?.Count == dialogue.Count ? null : new SubagentDialogueEntryViewModel[dialogue.Count];
        for (var index = 0; index < dialogue.Count; index++)
        {
            var message = dialogue[index];
            var retained = previous is not null && index < previous.Count
                && previous[index].Role == message.Role && previous[index].Content == message.Content;
            if (retained)
            {
                if (entries is not null) entries[index] = previous![index];
                continue;
            }
            if (entries is null)
            {
                entries = new SubagentDialogueEntryViewModel[dialogue.Count];
                for (var prefix = 0; prefix < index; prefix++) entries[prefix] = previous![prefix];
            }
            entries[index] = new SubagentDialogueEntryViewModel(message);
        }
        return entries ?? previous!;
    }
}

