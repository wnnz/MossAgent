using System.Text;

namespace MossAgent.App.Terminal;

public sealed class TerminalSelection
{
    public const int MaximumCharacters = 32768;
    private TerminalFrame? _frame;
    private int? _anchor;
    private int _active;
    public event EventHandler? Changed;
    public bool HasSelection => _anchor is not null && _frame is { Failed: false };
    private int Start => Math.Min(_anchor ?? 0, _active);
    private int End => Math.Max(_anchor ?? 0, _active);

    public void Observe(TerminalFrame? frame)
    {
        if (HasSelection && !Compatible(frame)) Clear();
        _frame = frame;
    }

    private bool Compatible(TerminalFrame? frame)
    {
        if (frame is null || _frame is null || frame.Failed || frame.ViewId != _frame.ViewId || frame.TaskId != _frame.TaskId
            || frame.SessionId != _frame.SessionId || frame.AlternateScreen != _frame.AlternateScreen
            || frame.Columns != _frame.Columns || frame.DisplayRows != _frame.DisplayRows) return false;
        for (var row = Start / frame.Columns; row <= End / frame.Columns; row++)
        {
            if (!frame.Rows[row].AsSpan().SequenceEqual(_frame.Rows[row].AsSpan())
                || Wraps(frame, row) != Wraps(_frame, row)) return false;
        }
        return true;
    }

    public void Begin(int row, int column)
    {
        if (_frame is not { Failed: false, DisplayRows: > 0 } frame) return;
        _anchor = Math.Clamp(row, 0, frame.DisplayRows - 1) * frame.Columns + Math.Clamp(column, 0, frame.Columns - 1);
        _active = _anchor.Value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Extend(int row, int column)
    {
        if (!HasSelection) return;
        var frame = _frame!;
        _active = Math.Clamp(row, 0, frame.DisplayRows - 1) * frame.Columns + Math.Clamp(column, 0, frame.Columns - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAll()
    {
        if (_frame is not { Failed: false, DisplayRows: > 0 } frame) return;
        _anchor = 0;
        _active = frame.DisplayRows * frame.Columns - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_anchor is null) return;
        _anchor = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Contains(int row, int column, int width)
    {
        if (!HasSelection) return false;
        var position = row * _frame!.Columns + column;
        return position <= End && position + Math.Max(1, width) > Start;
    }

    public string? GetText()
    {
        if (!HasSelection) return null;
        var frame = _frame!;
        var result = new StringBuilder();
        for (var rowIndex = Start / frame.Columns; rowIndex <= End / frame.Columns; rowIndex++)
        {
            var line = new StringBuilder();
            var row = frame.Rows[rowIndex];
            for (var column = 0; column < frame.Columns; column++)
            {
                var cell = row[column];
                if (cell.Width == 0 || !Contains(rowIndex, column, cell.Width)) continue;
                line.Append((cell.Attributes & 128) != 0 || cell.Text.Length == 0 ? " " : cell.Text);
                if (line.Length + result.Length > MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(MaximumCharacters));
            }
            var wrapped = Wraps(frame, rowIndex);
            result.Append(wrapped ? line.ToString() : line.ToString().TrimEnd(' '));
            if (rowIndex < End / frame.Columns && !wrapped) result.Append('\n');
            if (result.Length > MaximumCharacters) throw new ArgumentOutOfRangeException(nameof(MaximumCharacters));
        }
        return result.ToString();
    }

    private static bool Wraps(TerminalFrame frame, int row) => !frame.WrappedRows.IsDefaultOrEmpty && frame.WrappedRows[row];
}
