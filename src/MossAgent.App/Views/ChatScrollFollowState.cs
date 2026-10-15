namespace MossAgent.App.Views;

internal sealed class ChatScrollFollowState
{
    private const double BottomTolerance = 24;

    public bool IsFollowing { get; private set; } = true;

    public void Reset() => IsFollowing = true;

    public bool Update(
        double extentDelta,
        double viewportDelta,
        double offsetDelta,
        double distanceFromEnd)
    {
        if (extentDelta != 0 || viewportDelta != 0) return IsFollowing;
        if (offsetDelta != 0) IsFollowing = distanceFromEnd <= BottomTolerance;
        return false;
    }
}
