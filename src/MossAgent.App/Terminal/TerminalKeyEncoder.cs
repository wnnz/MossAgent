using System.Globalization;
using System.Text;
using Avalonia.Input;

namespace MossAgent.App.Terminal;

internal static class TerminalKeyEncoder
{
    internal const int MaximumCharacters = 32768;

    public static string? Encode(TerminalInputRequest input, TerminalFrame frame)
    {
        if (input.Text is { } text)
        {
            if (input.IsPaste) return TerminalPasteEncoder.Encode(text, frame);
            if (text.Length > MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(input));
            if (!frame.Win32InputMode) return text;
            var result = new StringBuilder();
            foreach (var character in text)
            {
                result.Append(Win32(0, character, KeyModifiers.None));
                if (result.Length > MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(input));
            }
            return result.ToString();
        }
        var value = Classic(input.Key, input.Modifiers, frame.ApplicationCursorKeys);
        if (value is null || !frame.Win32InputMode) return value;
        var virtualKey = VirtualKey(input.Key);
        return Win32(virtualKey, Win32Character(input.Key, input.Modifiers), input.Modifiers);
    }

    private static string? Classic(Key key, KeyModifiers modifiers, bool application)
    {
        if (modifiers.HasFlag(KeyModifiers.Meta)) return null;
        if (modifiers == (KeyModifiers.Control | KeyModifiers.Shift) && key is Key.C or Key.V) return null;
        var control = modifiers.HasFlag(KeyModifiers.Control);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        if (key is >= Key.A and <= Key.Z && (control ^ alt))
        {
            var character = control ? (char)(key - Key.A + 1) : (char)(key - Key.A + (shift ? 'A' : 'a'));
            return (alt ? "\u001b" : string.Empty) + character;
        }
        var simple = key switch
        {
            Key.Enter => "\r",
            Key.Back => control ? "\b" : "\u007f",
            Key.Escape => "\u001b",
            Key.Tab => shift ? "\u001b[Z" : "\t",
            Key.Space when control && !alt => "\0",
            Key.OemOpenBrackets when control && !alt => "\u001b",
            Key.OemPipe or Key.OemBackslash when control && !alt => "\u001c",
            Key.OemCloseBrackets when control && !alt => "\u001d",
            Key.D6 when control && !alt => "\u001e",
            Key.OemMinus when control && !alt => "\u001f",
            _ => null
        };
        if (simple is not null) return (alt ? "\u001b" : string.Empty) + simple;
        var modifier = 1 + (shift ? 1 : 0) + (alt ? 2 : 0) + (control ? 4 : 0);
        var final = key switch
        {
            Key.Up => "A",
            Key.Down => "B",
            Key.Right => "C",
            Key.Left => "D",
            Key.Home => "H",
            Key.End => "F",
            Key.F1 => "P",
            Key.F2 => "Q",
            Key.F3 => "R",
            Key.F4 => "S",
            _ => null
        };
        if (final is not null) return modifier != 1 ? $"\u001b[1;{modifier}{final}"
            : (application || key is >= Key.F1 and <= Key.F4 ? "\u001bO" : "\u001b[") + final;
        var number = key switch
        {
            Key.Insert => 2,
            Key.Delete => 3,
            Key.PageUp => 5,
            Key.PageDown => 6,
            Key.F5 => 15,
            Key.F6 => 17,
            Key.F7 => 18,
            Key.F8 => 19,
            Key.F9 => 20,
            Key.F10 => 21,
            Key.F11 => 23,
            Key.F12 => 24,
            _ => 0
        };
        return number == 0 ? null : modifier == 1 ? $"\u001b[{number}~" : $"\u001b[{number};{modifier}~";
    }

    private static int VirtualKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key - Key.A + 65,
        >= Key.D0 and <= Key.D9 => key - Key.D0 + 48,
        >= Key.F1 and <= Key.F12 => key - Key.F1 + 112,
        Key.Back => 8,
        Key.Tab => 9,
        Key.Enter => 13,
        Key.Escape => 27,
        Key.Space => 32,
        Key.PageUp => 33,
        Key.PageDown => 34,
        Key.End => 35,
        Key.Home => 36,
        Key.Left => 37,
        Key.Up => 38,
        Key.Right => 39,
        Key.Down => 40,
        Key.Insert => 45,
        Key.Delete => 46,
        Key.OemMinus => 189,
        Key.OemOpenBrackets => 219,
        Key.OemPipe => 220,
        Key.OemBackslash => 226,
        Key.OemCloseBrackets => 221,
        _ => 0
    };

    private static char Win32Character(Key key, KeyModifiers modifiers)
    {
        var control = modifiers.HasFlag(KeyModifiers.Control);
        if (key is >= Key.A and <= Key.Z) return control ? (char)(key - Key.A + 1)
            : (char)(key - Key.A + (modifiers.HasFlag(KeyModifiers.Shift) ? 'A' : 'a'));
        return key switch
        {
            Key.Enter => '\r',
            Key.Back => '\b',
            Key.Tab => '\t',
            Key.Escape => '\u001b',
            Key.Space => '\0',
            Key.OemOpenBrackets when control => '\u001b',
            Key.OemPipe or Key.OemBackslash when control => '\u001c',
            Key.OemCloseBrackets when control => '\u001d',
            Key.D6 when control => '\u001e',
            Key.OemMinus when control => '\u001f',
            _ => '\0'
        };
    }

    private static string Win32(int virtualKey, char character, KeyModifiers modifiers)
    {
        var state = (modifiers.HasFlag(KeyModifiers.Shift) ? 16 : 0) | (modifiers.HasFlag(KeyModifiers.Control) ? 8 : 0)
            | (modifiers.HasFlag(KeyModifiers.Alt) ? 2 : 0);
        return string.Create(CultureInfo.InvariantCulture, $"\u001b[{virtualKey};0;{(int)character};1;{state};1_");
    }
}
