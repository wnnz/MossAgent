namespace MossAgent.Tools.Abstractions.Browser;

public sealed record BrowserTabInfo(
    string Id,
    string Title,
    Uri? Uri,
    bool IsActive);
