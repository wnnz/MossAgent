using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolOutputLimiter
{
    public static ToolResult Limit(ToolResult result, int maximumCharacters)
    {
        var remaining = Math.Max(0, maximumCharacters);
        var changes = LimitFileChanges(result.FileChanges, ref remaining, out var truncated);
        var summary = LimitText(result.Summary, ref remaining) ?? string.Empty;
        var content = LimitText(result.Content, ref remaining);
        var dialogue = LimitDialogue(result.Dialogue, ref remaining);
        if (ReferenceEquals(changes, result.FileChanges) && ReferenceEquals(summary, result.Summary)
            && ReferenceEquals(content, result.Content) && ReferenceEquals(dialogue, result.Dialogue))
            return result;
        return result with
        {
            Summary = summary,
            Content = content,
            Dialogue = dialogue,
            FileChanges = changes,
            FileChangesTruncated = result.FileChangesTruncated || truncated
        };
    }

    private static IReadOnlyList<ToolFileChange>? LimitFileChanges(
        IReadOnlyList<ToolFileChange>? changes, ref int remaining, out bool truncated)
    {
        truncated = false;
        if (changes is null) return null;
        for (var index = 0; index < changes.Count; index++)
        {
            var change = changes[index];
            if (change is null || !change.IsValid || change.OutputCharacterCount > remaining)
            {
                truncated = true;
                return changes.Take(index).ToArray();
            }
            remaining -= change.OutputCharacterCount;
        }
        return changes;
    }

    private static string? LimitText(string? value, ref int remaining)
    {
        if (value is null) return null;
        if (value.Length <= remaining)
        {
            remaining -= value.Length;
            return value;
        }

        const string marker = "\n[截断]";
        var showMarker = remaining >= marker.Length;
        var retained = Math.Max(0, remaining - (showMarker ? marker.Length : 0));
        if (retained > 0 && char.IsHighSurrogate(value[retained - 1]) && char.IsLowSurrogate(value[retained]))
            retained--;
        var result = value[..retained];
        remaining = 0;
        return showMarker ? result + marker : result;
    }

    private static IReadOnlyList<ToolDialogueMessage>? LimitDialogue(
        IReadOnlyList<ToolDialogueMessage>? dialogue, ref int remaining)
    {
        if (dialogue is null) return null;
        for (var index = 0; index < dialogue.Count; index++)
        {
            if (remaining == 0) return dialogue.Take(index).ToArray();
            var message = dialogue[index];
            var role = LimitText(message.Role, ref remaining) ?? string.Empty;
            var content = LimitText(message.Content, ref remaining) ?? string.Empty;
            if (!ReferenceEquals(role, message.Role) || !ReferenceEquals(content, message.Content))
                return [.. dialogue.Take(index), message with { Role = role, Content = content }];
        }
        return dialogue;
    }
}
