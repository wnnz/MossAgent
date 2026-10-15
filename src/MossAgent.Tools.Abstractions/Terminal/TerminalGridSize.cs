namespace MossAgent.Tools.Abstractions.Terminal;

public sealed record TerminalGridSize
{
    public static TerminalGridSize Default { get; } = new(120, 30);
    public int Columns { get; }
    public int Rows { get; }

    public TerminalGridSize(int columns, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 20);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(columns, 500);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 5);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, 200);
        Columns = columns;
        Rows = rows;
    }
}
