using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Application.Persistence;

/// <summary>
/// One journaled task browser session identity. The record deliberately stores only the
/// engine, profile mode and lifecycle state — never the profile path, CDP endpoint,
/// proxy credentials or any page URL.
/// </summary>
public sealed record BrowserSessionRecord(
    Guid SessionId,
    Guid TaskId,
    BrowserEngine Engine,
    BrowserProfileMode ProfileMode,
    BrowserSessionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
