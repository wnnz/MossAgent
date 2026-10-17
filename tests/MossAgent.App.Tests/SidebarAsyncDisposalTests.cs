using System.Collections.ObjectModel;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarAsyncDisposalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RepeatedStopAndCancelledWaiters_DoNotAbandonStorage(bool synchronousDisposeFirst, bool read)
    {
        await using var probe = new SidebarStorageReleaseProbe();
        var store = new TestSidebarPreferenceStore
        {
            LoadHandler = read ? async token =>
            {
                await probe.BlockAsync(token);
                return new SidebarPreference(SidebarProjectSortOrder.Manual, []);
            }
            : null
        };
        ObservableCollection<WorkspaceProjectNavigationViewModel> source =
            [SidebarProjectOrderingTests.Project("first", 1), SidebarProjectOrderingTests.Project("second", 0)];
        var sidebar = new WorkspaceSidebarProjectsViewModel(source, store);
        try
        {
            var loading = sidebar.LoadAsync();
            Task pending;
            if (read) pending = loading;
            else
            {
                await loading;
                store.SaveHandler = (_, token) => probe.BlockAsync(token);
                pending = sidebar.SortManuallyCommand.ExecuteAsync(null);
            }
            await probe.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            if (synchronousDisposeFirst) sidebar.Dispose();
            var stopping = sidebar.StopAsync();
            Assert.Same(stopping, sidebar.StopAsync());
            Assert.Same(stopping, sidebar.DisposeAsync().AsTask());
            Assert.True(probe.Token.IsCancellationRequested);
            Assert.False(stopping.IsCompleted);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopping.WaitAsync(cancelled.Token));
            Assert.False(stopping.IsCompleted);
            Assert.Throws<IOException>(() => { using var competing = File.Open(probe.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None); });
            probe.Release.TrySetResult();
            await stopping.WaitAsync(TestContext.Current.CancellationToken);
            await pending.WaitAsync(TestContext.Current.CancellationToken);
            using var released = File.Open(probe.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(sidebar.IsActivitySort);
            Assert.Equal(SidebarProjectSortOrder.UpdatedAt, store.Saved.ProjectSortOrder);
        }
        finally
        {
            probe.Release.TrySetResult();
            await sidebar.DisposeAsync();
        }
    }
}
