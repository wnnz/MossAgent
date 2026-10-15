using Avalonia;

namespace MossAgent.App.Controls;

internal sealed record TerminalScrollPosition(Vector Offset, bool Follow, long Visit);
