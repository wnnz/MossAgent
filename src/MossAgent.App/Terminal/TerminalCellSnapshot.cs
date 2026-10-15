namespace MossAgent.App.Terminal;

public readonly record struct TerminalCellSnapshot(
    string Text, byte Width, uint Foreground, uint Background,
    bool DefaultForeground, bool DefaultBackground, byte Attributes,
    bool Underline, bool StrikeThrough, bool Overline);
