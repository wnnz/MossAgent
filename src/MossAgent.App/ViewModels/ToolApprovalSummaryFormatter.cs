using System.Globalization;
using System.Text.Json;
using MossAgent.App.Resources;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

internal static class ToolApprovalSummaryFormatter
{
    public static string Format(ToolDescriptor descriptor, ToolRequest request)
    {
        var arguments = request.Arguments;
        if (arguments.ValueKind != JsonValueKind.Object) return ApprovalText.Get("ArgumentsHidden");
        var lines = new List<string>();
        var name = descriptor.Name;
        if (name.StartsWith("filesystem.", StringComparison.Ordinal))
        {
            AddTarget(lines, arguments, "path", "Target");
            AddTarget(lines, arguments, "source", "Source");
            AddTarget(lines, arguments, "destination", "Destination");
            if (name == "filesystem.write_file") AddLength(lines, arguments, "content", "ContentLength");
            if (name == "filesystem.replace_text")
            {
                AddLength(lines, arguments, "oldText", "OldTextLength");
                AddLength(lines, arguments, "newText", "NewTextLength");
            }
        }
        else if (name is "shell.run" or "terminal.start")
        {
            AddTarget(lines, arguments, "workingDirectory", "CommandDirectory");
            AddLength(lines, arguments, "command", "CommandLength");
        }
        else if (name == "terminal.write") AddLength(lines, arguments, "text", "InputLength");
        else if (name.StartsWith("browser.", StringComparison.Ordinal))
        {
            if (arguments.TryGetProperty("url", out var value) && value.ValueKind == JsonValueKind.String
                && Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            {
                var safeUri = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty,
                    Query = string.Empty, Fragment = string.Empty }.Uri;
                lines.Add(ApprovalText.Format("Url", SafeText(safeUri.GetLeftPart(UriPartial.Path))));
            }
            AddLength(lines, arguments, "text", "InputLength");
        }
        return lines.Count == 0 ? ApprovalText.Get("ArgumentsHidden")
            : string.Join(Environment.NewLine, lines);
    }

    public static string SafeText(string? value, int limit = 512)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var length = Math.Min(value.Length, limit);
        if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
        var text = new string(value[..length].Select(character =>
            char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator ? ' ' : character).ToArray());
        return text.Trim() + (length < value.Length ? "…" : string.Empty);
    }

    private static void AddTarget(List<string> lines, JsonElement arguments, string property, string label)
    {
        if (arguments.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
            lines.Add(ApprovalText.Format(label, SafeText(value.GetString())));
    }

    private static void AddLength(List<string> lines, JsonElement arguments, string property, string label)
    {
        if (arguments.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
            lines.Add(ApprovalText.Format(label, value.GetString()?.Length ?? 0));
    }
}
