using System.Collections.Immutable;
using System.Text;
using MossAgent.Tools.Abstractions.Terminal;
using RoyalTerminal.Avalonia.Rendering;
using RoyalTerminal.Terminal;

namespace MossAgent.App.Terminal;

internal sealed class TerminalVtScreen : IDisposable
{
    internal const int Columns = 120;
    internal const int ViewportRows = 30;
    internal const int HistoryRows = 128;
    internal const int MaximumCells = 100_000;
    private readonly TerminalScreen _screen = new(Columns, ViewportRows, HistoryRows);
    private readonly BasicVtProcessor _processor;
    private readonly TerminalControlStringBoundary _boundary = new();
    private readonly Encoder _encoder = Encoding.UTF8.GetEncoder();
    private bool _hasOutput;
    private long _revision;
    public int EstimatedCells => _screen.Columns * (_screen.ViewportRows + _screen.ScrollbackLimit) * 2;
    public TerminalGridSize Grid => new(_screen.Columns, _screen.ViewportRows);

    public TerminalVtScreen()
    {
        _processor = new(_screen, new BasicVtProcessorOptions
        {
            SixelGraphicsEnabled = false,
            GlyphProtocolEnabled = false,
            TitleReportEnabled = false,
            ClipboardWriteLimitBytes = 0,
            KittyGraphicsMaxApcBytes = 0,
            KittyGraphicsMaxImageBytes = 0,
            KittyGraphicsStorageLimitBytes = 0,
            UnknownSequenceMaxBytes = 0,
            ContinuationMaxBytes = 4096,
            KittyGraphicsMediumReader = null,
            KittyGraphicsPngDecoder = null
        });
    }

    public void Process(string text)
    {
        Span<char> characters = stackalloc char[2048];
        Span<byte> bytes = stackalloc byte[8192];
        for (var offset = 0; offset < text.Length;)
        {
            var count = 0;
            var end = Math.Min(offset + 1024, text.Length);
            while (offset < end) count += _boundary.Append(text[offset++], characters[count..]);
            _encoder.Convert(characters[..count], bytes, false, out _, out var bytesUsed, out _);
            if (bytesUsed == 0) continue;
            _processor.Process(bytes[..bytesUsed]);
            _hasOutput = true;
        }
        _revision++;
    }

    public void Resize(TerminalGridSize size)
    {
        if (Grid == size) return;
        _screen.ScrollbackLimit = Math.Min(HistoryRows, Math.Max(0, MaximumCells / size.Columns - size.Rows));
        _processor.ResizeScreen(size.Columns, size.Rows, 0, 0);
        _screen.DiscardHiddenCells();
        _revision++;
    }

    public TerminalFrame Capture(Guid viewId, Guid taskId, Guid? sessionId, bool failed)
    {
        var rows = ImmutableArray.CreateBuilder<ImmutableArray<TerminalCellSnapshot>>(_screen.TotalRows);
        var wraps = ImmutableArray.CreateBuilder<bool>(_screen.TotalRows);
        var cursorRow = _screen.TotalRows - _screen.ViewportRows + _processor.CursorRow;
        var displayRows = _processor.AlternateScreen ? _screen.TotalRows : cursorRow + 1;
        for (var rowIndex = 0; rowIndex < _screen.TotalRows; rowIndex++)
        {
            var cells = _screen.GetRow(rowIndex).ReadOnlyCells;
            var copied = ImmutableArray.CreateBuilder<TerminalCellSnapshot>(_screen.Columns);
            foreach (ref readonly var cell in cells)
            {
                if (cell.HasContent || cell.BackgroundIdentity.Kind != default || cell.Attributes.HasFlag(CellAttributes.Inverse)) displayRows = Math.Max(displayRows, rowIndex + 1);
                var text = cell.Grapheme ?? (cell.Codepoint > 0 ? char.ConvertFromUtf32(cell.Codepoint) : string.Empty);
                copied.Add(new(text, cell.Width, cell.Foreground, cell.Background,
                    cell.ForegroundIdentity.Kind == default, cell.BackgroundIdentity.Kind == default,
                    (byte)cell.Attributes, cell.UnderlineStyle != TerminalUnderlineStyle.None
                        || cell.Attributes.HasFlag(CellAttributes.Underline),
                    cell.Attributes.HasFlag(CellAttributes.Strikethrough), cell.Decorations.HasFlag(CellDecorations.Overline)));
            }
            rows.Add(copied.MoveToImmutable());
            wraps.Add(_screen.GetRow(rowIndex).WrapsToNext);
        }
        return new(viewId, taskId, sessionId, _revision, _screen.Columns, _screen.ViewportRows, Math.Max(displayRows, cursorRow + 1), rows.MoveToImmutable(),
            _processor.CursorCol, cursorRow,
            _processor.CursorVisible, _processor.AlternateScreen, _hasOutput, _screen.TotalRows == _screen.ScrollbackLimit + _screen.ViewportRows,
            failed, _processor.ApplicationCursorKeys, _processor.BracketedPaste, _processor.Win32InputMode, wraps.MoveToImmutable());
    }

    public void Dispose() => _processor.Dispose();
}
