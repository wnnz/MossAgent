namespace MossAgent.Tools.Abstractions.Processes;

public sealed record TaskProcessStartRequest(
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    bool RedirectStandardInput = false,
    IReadOnlyDictionary<string, string?>? Environment = null);
