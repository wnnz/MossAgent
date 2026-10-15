using Avalonia.Input;

namespace MossAgent.App.Terminal;

public sealed record TerminalInputRequest(Guid ViewId, Guid SessionId, string? Text = null,
    Key Key = Key.None, KeyModifiers Modifiers = KeyModifiers.None, bool IsPaste = false, int? ScopeGeneration = null);
