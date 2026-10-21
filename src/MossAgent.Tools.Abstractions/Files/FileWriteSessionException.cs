namespace MossAgent.Tools.Abstractions.Files;

/// <summary>A known refusal with a localized, secret-free message safe for tool results.</summary>
public sealed class FileWriteSessionException(string message, string errorCode = "file_write_unsafe")
    : FileMutationSessionException(message, errorCode);
