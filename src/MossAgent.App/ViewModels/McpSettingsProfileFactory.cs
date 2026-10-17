using System.Diagnostics.CodeAnalysis;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class McpSettingsProfileFactory
{
    public static bool TryCreate(McpSettingsViewModel editor, Guid editorId,
        [NotNullWhen(true)] out McpServerProfile? profile, out string error)
    {
        profile = null;
        var arguments = editor.IsStdioTransport ? editor.ArgumentsJson : "[]";
        var environment = editor.IsStdioTransport ? editor.EnvironmentJson : "{}";
        var headers = editor.IsHttpTransport ? editor.HeadersJson : "{}";
        if (!McpSettingsValidator.TryValidate(editor.Name, editor.Transport, editor.Command, editor.Url,
                arguments, environment, headers, out var endpoint, out error)) return false;
        profile = new McpServerProfile(editorId, editor.Name.Trim(), editor.Transport,
            editor.IsStdioTransport ? EmptyToNull(editor.Command) : null, arguments,
            editor.IsStdioTransport ? EmptyToNull(editor.WorkingDirectory) : null, environment,
            endpoint, headers, editor.IsHttpTransport ? editor.SelectedProxy?.Id : null, editor.IsEnabled);
        return true;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
