using Avalonia;
using Avalonia.Input.TextInput;

namespace MossAgent.App.Controls;

internal sealed class TerminalInputMethodClient(Visual visual, Func<bool> available,
    Func<Rect> cursorRectangle, Action<string> present) : TextInputMethodClient
{
    internal const int MaximumPreeditCharacters = 1024;
    private bool _valid = true;
    public bool IsComposing { get; private set; }
    public bool IsAvailable => _valid && available();
    public int Cursor { get; private set; }
    public override Visual TextViewVisual => visual;
    public override bool SupportsPreedit => true;
    public override bool SupportsSurroundingText => false;
    public override string SurroundingText => string.Empty;
    public override Rect CursorRectangle => _valid && available() ? cursorRectangle() : default;
    public override TextSelection Selection { get => default; set { } }

    public override void SetPreeditText(string? text) => SetPreeditText(text, null);

    public override void SetPreeditText(string? text, int? cursorPos)
    {
        if (!_valid) return;
        if (!available()) { Invalidate(); return; }
        text ??= string.Empty;
        if (text.Length > MaximumPreeditCharacters || text.Any(char.IsControl))
        { Clear(true); return; }
        Cursor = Math.Clamp(cursorPos ?? text.Length, 0, text.Length);
        if (Cursor > 0 && Cursor < text.Length && char.IsHighSurrogate(text[Cursor - 1]) && char.IsLowSurrogate(text[Cursor])) Cursor--;
        IsComposing = text.Length > 0;
        present(text);
        RaiseCursorRectangleChanged();
    }

    public void Clear(bool reset)
    {
        if (!_valid) return;
        IsComposing = false;
        Cursor = 0;
        present(string.Empty);
        if (reset) RequestReset();
        RaiseCursorRectangleChanged();
    }

    public void Invalidate()
    {
        if (!_valid) return;
        _valid = false;
        IsComposing = false;
        Cursor = 0;
        present(string.Empty);
        RequestReset();
        RaiseCursorRectangleChanged();
    }

    public void RefreshCursor() { if (_valid) RaiseCursorRectangleChanged(); }
}
