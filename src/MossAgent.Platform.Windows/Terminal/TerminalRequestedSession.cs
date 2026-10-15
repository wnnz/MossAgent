using MossAgent.Tools.Abstractions;

namespace MossAgent.Platform.Windows.Terminal;

internal static class TerminalRequestedSession
{
    public static Guid? Read(ToolRequest request)
    {
        var text = request.Arguments.GetOptionalString("sessionId");
        if (text is null) return null;
        return Guid.TryParseExact(text, "D", out var session) ? session
            : throw new ArgumentException("Invalid terminal session identifier.");
    }
}
