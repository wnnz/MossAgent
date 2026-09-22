namespace MossAgent.Tools.Abstractions;

[Flags]
public enum ToolCapability
{
    None = 0,
    FileRead = 1,
    FileWrite = 2,
    Process = 4,
    Network = 8,
    BrowserRead = 16,
    BrowserWrite = 32,
    ScreenCapture = 64
}

