namespace MossAgent.App.Tests;

internal static class AllocationMeasurement
{
    internal static long Measure(Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!GC.TryStartNoGCRegion(16 * 1024 * 1024))
            throw new InvalidOperationException("Could not isolate the allocation measurement from garbage collection.");

        try
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            operation();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        finally
        {
            GC.EndNoGCRegion();
        }
    }
}
