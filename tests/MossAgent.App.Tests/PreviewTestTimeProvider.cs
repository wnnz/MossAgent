namespace MossAgent.App.Tests;

internal sealed class PreviewTestTimeProvider(DateTimeOffset now) : TimeProvider
{
    public int UtcNowReads { get; private set; }

    public override DateTimeOffset GetUtcNow()
    {
        UtcNowReads++;
        return now;
    }
}
