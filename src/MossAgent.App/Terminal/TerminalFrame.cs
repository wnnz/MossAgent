using System.Collections.Immutable;

namespace MossAgent.App.Terminal;

public sealed record TerminalFrame(
    Guid ViewId, Guid TaskId, Guid? SessionId, long Revision,
    int Columns, int ViewportRows, int DisplayRows, ImmutableArray<ImmutableArray<TerminalCellSnapshot>> Rows,
    int CursorColumn, int CursorRow, bool CursorVisible, bool AlternateScreen,
    bool HasOutput, bool HistoryLimited, bool Failed,
    bool ApplicationCursorKeys = false, bool BracketedPaste = false, bool Win32InputMode = false,
    ImmutableArray<bool> WrappedRows = default);
