namespace MossAgent.Tools.Abstractions.Files;

public sealed class FileDeleteSessionException(string message, string errorCode = "file_delete_unsafe")
    : FileMutationSessionException(message, errorCode, errorCode == "file_delete_incomplete");
