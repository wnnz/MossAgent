using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MossAgent.App.Terminal;

namespace MossAgent.App.Controls;

internal sealed class TerminalGlyphCache
{
    internal const int MaximumEntries = 512;
    private readonly Dictionary<(string Text, uint Color, byte Style), FormattedText> _glyphs = [];
    private FontFamily? _family;
    private double _size;
    private Color _foreground;

    public int Count => _glyphs.Count;

    public void Reset(FontFamily family, double size, Color foreground)
    {
        if (_family == family && _size == size && _foreground == foreground) return;
        _family = family;
        _size = size;
        _foreground = foreground;
        _glyphs.Clear();
    }

    public FormattedText Get(TerminalCellSnapshot cell, Color color)
    {
        var key = (cell.Text, color.ToUInt32(), (byte)(cell.Attributes & 3));
        if (_glyphs.TryGetValue(key, out var cached)) return cached;
        if (_glyphs.Count == MaximumEntries) _glyphs.Clear();
        var typeface = new Typeface(_family!, (cell.Attributes & 2) != 0 ? FontStyle.Italic : FontStyle.Normal,
            (cell.Attributes & 1) != 0 ? FontWeight.Bold : FontWeight.Normal);
        var glyph = new FormattedText(cell.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, _size, new ImmutableSolidColorBrush(color));
        _glyphs.Add(key, glyph);
        return glyph;
    }

    public void Clear() => _glyphs.Clear();
}
