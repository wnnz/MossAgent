using MossAgent.Platform.Windows.Windowing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DesktopApplicationInstanceTests
{
    private static string Root(string name) =>
        Path.Combine(Path.GetTempPath(), "MossAgent.Tests", "instance", name);

    // Runs Create+Dispose of the secondary on one dedicated thread so mutex
    // ownership is always released by its owning thread.
    private static bool OnWorker(Func<DesktopApplicationInstance, bool> work, string root)
    {
        Exception? failure = null;
        var result = false;
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            DesktopApplicationInstance? secondary = null;
            try { secondary = DesktopApplicationInstance.Create(root); result = work(secondary); }
            catch (Exception exception) { failure = exception; }
            finally
            {
                try { secondary?.Dispose(); } catch (Exception exception) { failure ??= exception; }
                done.Set();
            }
        }) { IsBackground = true };
        thread.Start();
        Assert.True(done.Wait(TimeSpan.FromSeconds(15)) && thread.Join(TimeSpan.FromSeconds(15)));
        if (failure is not null) throw failure;
        return result;
    }

    private static bool ReacquiredIsPrimary(string root) =>
        OnWorker(instance => instance.IsPrimary, root);

    [Fact]
    public void SecondarySameRootIsRejectedAndActivationReachesPrimary()
    {
        var root = Root(Guid.NewGuid().ToString("N"));
        using var primary = DesktopApplicationInstance.Create(root);
        Assert.True(primary.IsPrimary);
        var activated = new ManualResetEventSlim();
        primary.ActivationRequested += activated.Set;
        Assert.True(OnWorker(instance => { Assert.False(instance.IsPrimary); instance.SignalActivation(); return true; }, root));
        Assert.True(activated.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AliasedRootSharesTheSameSlot()
    {
        var directory = Root(Guid.NewGuid().ToString("N") + "CaseMiXeD");
        var alias = directory.ToLowerInvariant() + Path.DirectorySeparatorChar;
        using var primary = DesktopApplicationInstance.Create(directory.ToUpperInvariant());
        Assert.True(primary.IsPrimary);
        Assert.False(OnWorker(instance => instance.IsPrimary, alias));
    }

    [Fact]
    public void DistinctRootsAreIndependent()
    {
        using var first = DesktopApplicationInstance.Create(Root(Guid.NewGuid().ToString("N")));
        Assert.True(first.IsPrimary);
        Assert.True(ReacquiredIsPrimary(Root(Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void CrossThreadDisposeThrowsAndOwnerStillReleases()
    {
        var root = Root(Guid.NewGuid().ToString("N"));
        using var primary = DesktopApplicationInstance.Create(root);
        Assert.True(primary.IsPrimary);
        using var threw = new ManualResetEventSlim();
        var thread = new Thread(() => { try { primary.Dispose(); } catch (InvalidOperationException) { threw.Set(); } }) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        Assert.True(threw.IsSet);
        primary.Dispose();
        Assert.True(ReacquiredIsPrimary(root));
    }
}
