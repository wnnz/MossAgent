namespace MossAgent.Domain;

public sealed class SourceReviewException(string errorCode) : Exception("Source review operation failed.")
{
    public string ErrorCode { get; } = errorCode;
}
