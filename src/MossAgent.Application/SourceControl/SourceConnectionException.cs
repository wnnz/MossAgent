namespace MossAgent.Application.SourceControl;

public sealed class SourceConnectionException(SourceConnectionFailure failure)
    : Exception($"Source connection failed: {failure}.")
{
    public SourceConnectionFailure Failure { get; } = failure;
}
