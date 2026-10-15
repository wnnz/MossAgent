using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal sealed class SubagentTextPreview(Action<ToolDialogueMessage>? report)
{
    private const int MaximumCharacters = 1_024;
    private const int UpdateCharacters = 64;
    private int _lastLength;
    private string? _lastContent;
    private bool _wasTruncated;

    public void Report(StringBuilder text, bool force = false)
    {
        if (report is null || text.Length == 0) return;
        var length = Math.Min(text.Length, MaximumCharacters);
        if (char.IsHighSurrogate(text[length - 1])) length--;
        if (length == 0) return;
        var truncated = text.Length > MaximumCharacters;
        if (!force && _lastContent is not null && length - _lastLength < UpdateCharacters
            && truncated == _wasTruncated) return;

        var content = text.ToString(0, length) + (truncated ? "\n[预览已截断]" : string.Empty);
        if (content == _lastContent) return;
        report(new ToolDialogueMessage("子代理", content, IsPartial: true));
        _lastLength = length;
        _lastContent = content;
        _wasTruncated = truncated;
    }

    public static ToolDialogueMessage CreateMessage(string role, string content)
    {
        var length = Math.Min(content.Length, MaximumCharacters);
        if (length > 0 && char.IsHighSurrogate(content[length - 1])) length--;
        var preview = content[..length]
            + (content.Length > MaximumCharacters ? "\n[预览已截断]" : string.Empty);
        return new ToolDialogueMessage(role, preview);
    }
}
