namespace MossAgent.Infrastructure.Git;

public sealed record GitProcessResult(int ExitCode, string StandardOutput, string StandardError, bool OutputTruncated)
{
    public string Output => string.Join(Environment.NewLine, StandardOutput, StandardError).Trim();
}
