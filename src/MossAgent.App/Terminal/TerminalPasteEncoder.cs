using System.Text;
using Avalonia.Input;

namespace MossAgent.App.Terminal;

internal static class TerminalPasteEncoder
{
    public static string Encode(string text, TerminalFrame frame)
    {
        if (text.Length > TerminalKeyEncoder.MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(text));
        Validate(text);
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.Length == 0) return string.Empty;
        var content = frame.BracketedPaste ? "\u001b[200~" + normalized + "\u001b[201~" : normalized.Replace('\n', '\r');
        if (!frame.Win32InputMode)
        {
            if (content.Length > TerminalKeyEncoder.MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(text));
            return content;
        }
        var result = new StringBuilder();
        foreach (var character in content)
        {
            var request = character == '\r' ? new TerminalInputRequest(frame.ViewId, frame.SessionId!.Value, Key: Key.Enter)
                : new(frame.ViewId, frame.SessionId!.Value, Text: character.ToString());
            var encoded = TerminalKeyEncoder.Encode(request, frame)!;
            result.Append(encoded);
            result.Append(encoded.Replace(";1;0;1_", ";0;0;1_", StringComparison.Ordinal));
            if (result.Length > TerminalKeyEncoder.MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(text));
        }
        return result.ToString();
    }

    private static void Validate(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character < ' ' && character is not '\r' and not '\n' and not '\t' || character is >= '\u007f' and <= '\u009f')
                throw new ArgumentException("Terminal paste contains control characters.", nameof(text));
            if (!char.IsSurrogate(character)) continue;
            if (!char.IsHighSurrogate(character) || index + 1 >= text.Length || !char.IsLowSurrogate(text[++index]))
                throw new ArgumentException("Terminal paste contains invalid Unicode.", nameof(text));
        }
    }
}
