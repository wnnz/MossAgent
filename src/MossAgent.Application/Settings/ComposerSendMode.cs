namespace MossAgent.Application.Settings;

/// <summary>
/// Controls which keyboard shortcut submits the workspace composer.
/// </summary>
public enum ComposerSendMode
{
    /// <summary>Enter sends; Ctrl+Enter steers an active run or falls back to sending.</summary>
    EnterSends,

    /// <summary>Ctrl+Enter sends; Enter inserts a new line and the steering button remains available.</summary>
    ControlEnterSends
}
