using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileEditException(string errorCode, string resourceKey)
    : IOException(FileToolText.Get(resourceKey))
{
    public string ErrorCode { get; } = errorCode;
}
