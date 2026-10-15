using System.Globalization;
using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceToolProgressText
{
    public static string Status(ToolExecutionPhase phase) => WorkspaceText.Get($"ToolPhase{phase}");

    public static string Summary(string toolName, ToolExecutionPhase phase)
    {
        var displayName = DisplayName(toolName);
        return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get($"ToolPhase{phase}Summary"), displayName);
    }

    private static string DisplayName(string toolName)
    {
        Span<char> buffer = stackalloc char[160];
        var runeCount = 0;
        var written = 0;
        foreach (var rune in toolName.EnumerateRunes())
        {
            if (runeCount++ == 80) break;
            if (!Rune.IsControl(rune)) written += rune.EncodeToUtf16(buffer[written..]);
        }
        var displayName = buffer[..written];
        if (displayName.Trim().IsEmpty) return WorkspaceText.Get("ToolProgressGenericName");
        return displayName.SequenceEqual(toolName) ? toolName : new string(displayName);
    }
}
