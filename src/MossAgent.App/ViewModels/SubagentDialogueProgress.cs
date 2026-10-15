using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

internal static class SubagentDialogueProgress
{
    public static IReadOnlyList<ToolDialogueMessage> Apply(
        IReadOnlyList<ToolDialogueMessage>? previous, ToolDialogueMessage message)
    {
        var count = previous?.Count ?? 0;
        if (count > 0 && previous![^1].IsPartial && previous[^1].Role == message.Role) count--;
        var dialogue = new ToolDialogueMessage[count + 1];
        for (var index = 0; index < count; index++) dialogue[index] = previous![index];
        dialogue[count] = message;
        return dialogue;
    }
}
