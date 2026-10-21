namespace MossAgent.Tools.Abstractions.Files;

/// <summary>A known file-operation refusal with a localized, secret-free message safe for tool results.</summary>
public class FileMutationSessionException(string message, string errorCode, bool hasSideEffects = false) : IOException(message)
{
    public string ErrorCode { get; } = errorCode;
    public bool HasSideEffects { get; } = hasSideEffects;
}
