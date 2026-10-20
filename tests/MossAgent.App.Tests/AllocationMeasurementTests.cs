using System.Runtime;
using Xunit;

namespace MossAgent.App.Tests;

[Collection(AllocationMeasurementCollection.Name)]
public sealed class AllocationMeasurementTests
{
    [Fact]
    public void EmptyOperation_DoesNotAllocate()
    {
        var allocated = AllocationMeasurement.Measure(static () => Thread.SpinWait(100));

        Assert.Equal(0, allocated);
        AssertReleased();
    }

    [Fact]
    public void RealAllocation_IsStillCounted()
    {
        byte[]? retained = null;

        var allocated = AllocationMeasurement.Measure(() => retained = new byte[37]);

        Assert.NotNull(retained);
        Assert.True(allocated >= retained.Length);
        GC.KeepAlive(retained);
        AssertReleased();
    }

    [Fact]
    public void ThrowingOperation_PropagatesAndReleasesTheRegion()
    {
        var expected = new InvalidOperationException("fixture failure");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            AllocationMeasurement.Measure(() => throw expected));

        Assert.Same(expected, actual);
        AssertReleased();
    }

    [Fact]
    public void NestedMeasurement_IsRejectedAndReleasesTheOuterRegion()
    {
        Assert.Throws<InvalidOperationException>(() => AllocationMeasurement.Measure(() =>
            AllocationMeasurement.Measure(static () => { })));

        AssertReleased();
    }

    [Fact]
    public void NullOperation_IsRejectedBeforeStartingTheRegion()
    {
        Assert.Throws<ArgumentNullException>(() => AllocationMeasurement.Measure(null!));

        AssertReleased();
    }

    [Fact]
    public void ForcedCollection_InvalidatesTheMeasurementInsteadOfReturningAResult()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AllocationMeasurement.Measure(static () => GC.Collect()));

        AssertReleased();
    }

    private static void AssertReleased()
    {
        Assert.NotEqual(GCLatencyMode.NoGCRegion, GCSettings.LatencyMode);
        Assert.Equal(0, AllocationMeasurement.Measure(static () => { }));
    }
}
