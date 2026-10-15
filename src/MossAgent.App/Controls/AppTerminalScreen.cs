using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MossAgent.App.Terminal;

namespace MossAgent.App.Controls;

public sealed class AppTerminalScreen : Control
{
    public static readonly StyledProperty<TerminalFrame?> FrameProperty =
        AvaloniaProperty.Register<AppTerminalScreen, TerminalFrame?>(nameof(Frame));
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextBlock.FontFamilyProperty.AddOwner<AppTerminalScreen>();
    public static readonly StyledProperty<double> FontSizeProperty = TextBlock.FontSizeProperty.AddOwner<AppTerminalScreen>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextBlock.ForegroundProperty.AddOwner<AppTerminalScreen>();
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<AppTerminalScreen>();
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<AppTerminalScreen, IBrush?>(nameof(SelectionBrush));
    public static readonly StyledProperty<double> CellHeightProperty =
        AvaloniaProperty.Register<AppTerminalScreen, double>(nameof(CellHeight), 18, validate: value => double.IsFinite(value) && value > 0);
    private readonly TerminalGlyphCache _glyphs = new();
    private double _cellWidth;
    internal double CellWidth => _cellWidth;
    private Rect? _viewport;
    private IPointer? _selectionPointer;
    private Point _selectionOrigin;
    private bool _selectionMoved;
    internal int CachedGlyphCount => _glyphs.Count;

    static AppTerminalScreen()
    {
        AffectsMeasure<AppTerminalScreen>(FrameProperty, FontFamilyProperty, FontSizeProperty, CellHeightProperty);
        AffectsRender<AppTerminalScreen>(FrameProperty, FontFamilyProperty, FontSizeProperty, CellHeightProperty, ForegroundProperty, BackgroundProperty, SelectionBrushProperty);
    }

    public AppTerminalScreen()
    {
        Classes.Add("app-terminal-screen");
        ClipToBounds = true;
        Selection.Changed += (_, _) => InvalidateVisual();
    }
    public TerminalSelection Selection { get; } = new();
    public TerminalFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? SelectionBrush { get => GetValue(SelectionBrushProperty); set => SetValue(SelectionBrushProperty, value); }
    public double CellHeight { get => GetValue(CellHeightProperty); set => SetValue(CellHeightProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var foreground = (Foreground as ISolidColorBrush)?.Color ?? Colors.Gray;
        _glyphs.Reset(FontFamily, FontSize, foreground);
        _cellWidth = _glyphs.Get(new("M", 1, 0, 0, true, true, 0, false, false, false), foreground).Width;
        return Frame is { } frame ? new(frame.Columns * _cellWidth, frame.DisplayRows * CellHeight) : default;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is not { } frame || _cellWidth <= 0) return;
        var foreground = (Foreground as ISolidColorBrush)?.Color ?? Colors.Gray;
        var background = (Background as ISolidColorBrush)?.Color ?? Colors.Transparent;
        _glyphs.Reset(FontFamily, FontSize, foreground);
        if (Background is { } brush) context.FillRectangle(brush, new Rect(Bounds.Size));
        var startRow = Math.Clamp((int)Math.Floor((_viewport?.Y ?? 0) / CellHeight), 0, frame.DisplayRows);
        var endRow = Math.Min(frame.DisplayRows, (int)Math.Ceiling((_viewport?.Bottom ?? Bounds.Height) / CellHeight));
        var startColumn = Math.Clamp((int)Math.Floor((_viewport?.X ?? 0) / _cellWidth), 0, frame.Columns);
        var endColumn = Math.Min(frame.Columns, (int)Math.Ceiling((_viewport?.Right ?? Bounds.Width) / _cellWidth));
        for (var rowIndex = startRow; rowIndex < endRow; rowIndex++)
        {
            var row = frame.Rows[rowIndex];
            for (var columnIndex = Math.Max(0, startColumn - 1); columnIndex < endColumn; columnIndex++)
                DrawBackground(context, row[columnIndex], columnIndex, rowIndex, foreground, background);
            DrawSelection(context, row, rowIndex);
            for (var columnIndex = Math.Max(0, startColumn - 1); columnIndex < endColumn; columnIndex++)
                DrawCell(context, row[columnIndex], columnIndex, rowIndex, foreground, background);
        }
        if (frame.CursorVisible && frame.CursorColumn < frame.Columns && frame.CursorRow < frame.Rows.Length)
            context.DrawRectangle(null, new Pen(Foreground, 1), new Rect(
                frame.CursorColumn * _cellWidth + 0.5, frame.CursorRow * CellHeight + 0.5, _cellWidth - 1, CellHeight - 1));
    }

    internal void SetViewport(Vector offset, Size viewport)
    {
        _viewport = new Rect(new Point(offset.X, offset.Y), viewport);
        InvalidateVisual();
    }

    private void DrawCell(DrawingContext context, TerminalCellSnapshot cell, int column, int row, Color foreground, Color background)
    {
        if (cell.Width == 0) return;
        var cellForeground = cell.DefaultForeground ? foreground : Color.FromUInt32(cell.Foreground);
        var cellBackground = cell.DefaultBackground ? background : Color.FromUInt32(cell.Background);
        if ((cell.Attributes & 16) != 0) (cellForeground, cellBackground) = (cellBackground, cellForeground);
        var bounds = new Rect(column * _cellWidth, row * CellHeight, Math.Max(1, (int)cell.Width) * _cellWidth, CellHeight);
        if (cell.Width == 0 || cell.Text.Length == 0 || (cell.Attributes & 128) != 0) return;
        if ((cell.Attributes & 64) != 0) cellForeground = Color.FromArgb((byte)(cellForeground.A * 0.6), cellForeground.R, cellForeground.G, cellForeground.B);
        var glyph = _glyphs.Get(cell, cellForeground);
        using var clip = context.PushClip(bounds);
        context.DrawText(glyph, new Point(bounds.X, bounds.Y + (CellHeight - glyph.Height) / 2));
        var pen = new Pen(new ImmutableSolidColorBrush(cellForeground), 1);
        if (cell.Underline) context.DrawLine(pen, new(bounds.X, bounds.Bottom - 2), new(bounds.Right, bounds.Bottom - 2));
        if (cell.StrikeThrough) context.DrawLine(pen, new(bounds.X, bounds.Center.Y), new(bounds.Right, bounds.Center.Y));
        if (cell.Overline) context.DrawLine(pen, new(bounds.X, bounds.Y + 1), new(bounds.Right, bounds.Y + 1));
    }

    private void DrawBackground(DrawingContext context, TerminalCellSnapshot cell, int column, int row, Color foreground, Color background)
    {
        if (cell.Width == 0) return;
        var color = (cell.Attributes & 16) != 0 ? cell.DefaultForeground ? foreground : Color.FromUInt32(cell.Foreground)
            : cell.DefaultBackground ? background : Color.FromUInt32(cell.Background);
        if (color != background) context.FillRectangle(new ImmutableSolidColorBrush(color),
            new Rect(column * _cellWidth, row * CellHeight, cell.Width * _cellWidth, CellHeight));
    }

    private void DrawSelection(DrawingContext context, System.Collections.Immutable.ImmutableArray<TerminalCellSnapshot> row, int rowIndex)
    {
        if (!Selection.HasSelection || SelectionBrush is not { } brush) return;
        var first = row.Length;
        var last = 0;
        for (var column = 0; column < row.Length; column++)
        {
            if (row[column].Width == 0 || !Selection.Contains(rowIndex, column, row[column].Width)) continue;
            first = Math.Min(first, column);
            last = Math.Max(last, column + row[column].Width);
        }
        if (last > first) context.FillRectangle(brush, new Rect(first * _cellWidth, rowIndex * CellHeight, (last - first) * _cellWidth, CellHeight));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        ResetSelection();
        _glyphs.Clear();
        base.OnDetachedFromVisualTree(eventArgs);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrameProperty) Selection.Observe(Frame);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs eventArgs)
    {
        base.OnPointerPressed(eventArgs);
        if (eventArgs.Handled || !IsEffectivelyVisible || !IsEffectivelyEnabled || _cellWidth <= 0
            || !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _selectionOrigin = eventArgs.GetPosition(this);
        _selectionMoved = false;
        Selection.Begin((int)(_selectionOrigin.Y / CellHeight), (int)(_selectionOrigin.X / _cellWidth));
        _selectionPointer = eventArgs.Pointer;
        eventArgs.Pointer.Capture(this);
        eventArgs.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);
        if (_selectionPointer != eventArgs.Pointer || !IsEffectivelyVisible || !IsEffectivelyEnabled || _cellWidth <= 0) return;
        var position = eventArgs.GetPosition(this);
        _selectionMoved |= position != _selectionOrigin;
        Selection.Extend((int)Math.Floor(position.Y / CellHeight), (int)Math.Floor(position.X / _cellWidth));
        eventArgs.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);
        if (_selectionPointer != eventArgs.Pointer) return;
        if (!_selectionMoved) Selection.Clear();
        _selectionPointer = null;
        eventArgs.Pointer.Capture(null);
        eventArgs.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs eventArgs)
    {
        base.OnPointerCaptureLost(eventArgs);
        if (_selectionPointer is not null) ResetSelection();
    }

    internal void ResetSelection()
    {
        var pointer = _selectionPointer;
        _selectionPointer = null;
        pointer?.Capture(null);
        Selection.Clear();
    }
}
