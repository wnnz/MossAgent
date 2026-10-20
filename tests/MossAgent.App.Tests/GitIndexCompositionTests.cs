using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class GitIndexCompositionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task KeyboardActionsUseProductionPipelineRefreshFiltersAndKeepUnselectedChanges() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await GitIndexAppFixture.CreateAsync();
        var selected = "新文件 [1].txt";
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, selected), "selected\n", fixture.Token);
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "unselected.txt"), "preserved\n", fixture.Token);
        var head = await fixture.GitAsync("rev-parse", "HEAD");
        await using var pane = new DiffPaneViewModel(fixture.Tools);
        pane.Attach(fixture.Context);
        await pane.RefreshCommand.ExecuteAsync(null);
        pane.SelectedFile = Assert.Single(pane.Files, file => file.Path == selected);
        var view = new GitIndexActionsView { DataContext = pane.IndexActions };
        var window = new AppWindow { Content = view, Width = 300, Height = 160 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), item => item.Command == pane.IndexActions.ExecuteCommand);
            Activate(button);
            await pane.IndexActions.ExecuteCommand.ExecutionTask!;
            Assert.Equal(selected + "\0", await fixture.GitAsync("diff", "--cached", "--name-only", "-z"));
            Assert.DoesNotContain(pane.Files, file => file.Path == selected);
            Assert.Contains(pane.Files, file => file.Path == "unselected.txt");
            pane.IsStaged = true;
            Assert.Equal(selected, Assert.Single(pane.Files).Path);
            Assert.True(pane.IndexActions.CanExecute);
            window.UpdateLayout();
            Activate(button);
            await pane.IndexActions.ExecuteCommand.ExecutionTask!;
            Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
            Assert.Empty(pane.Files);
            pane.IsStaged = false;
            Assert.Contains(pane.Files, file => file.Path == selected);
            Assert.Contains(pane.Files, file => file.Path == "unselected.txt");
            await fixture.WaitAsync(() => !pane.IsDiffLoading);
            Assert.Equal("selected\n", await File.ReadAllTextAsync(Path.Combine(fixture.DirectoryPath, selected), fixture.Token));
            Assert.Equal("preserved\n", await File.ReadAllTextAsync(Path.Combine(fixture.DirectoryPath, "unselected.txt"), fixture.Token));
            Assert.Equal(head, await fixture.GitAsync("rev-parse", "HEAD"));
            var audits = await fixture.Audit.GetRecentAsync(fixture.Context.TaskId, 50, fixture.Token);
            var writes = audits.Where(audit => audit.ToolName is "git.stage_file" or "git.unstage_file").ToArray();
            Assert.Equal(2, writes.Length);
            Assert.All(writes, audit => { Assert.True(audit.IsSuccess); Assert.True(audit.ApprovalGranted); Assert.NotNull(audit.CompletedAt); });
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Activate(AppButton button)
    {
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    [Fact]
    public Task UnstagingACopyKeepsItsSourcesIndependentStagedEdit() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await GitIndexAppFixture.CreateAsync();
        File.Copy(Path.Combine(fixture.DirectoryPath, "tracked.txt"), Path.Combine(fixture.DirectoryPath, "copy.txt"));
        await File.WriteAllTextAsync(Path.Combine(fixture.DirectoryPath, "tracked.txt"), "independent source edit\n", fixture.Token);
        await fixture.GitAsync("add", "--", "tracked.txt", "copy.txt");
        var refreshes = 0;
        await using var actions = new GitIndexActionsViewModel(fixture.Tools, _ => { refreshes++; return Task.CompletedTask; });
        actions.Attach(fixture.Context);
        actions.Select(new("copy.txt", "C ", true, false, "tracked.txt"), true);
        await actions.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal("tracked.txt\0", await fixture.GitAsync("diff", "--cached", "--name-only", "-z"));
        Assert.Contains("independent source edit", await fixture.GitAsync("diff", "--cached", "--", "tracked.txt"));
        Assert.Equal("baseline\n", await File.ReadAllTextAsync(Path.Combine(fixture.DirectoryPath, "copy.txt"), fixture.Token));
        Assert.Equal(1, refreshes);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FileRemovedAfterStatusReportsFailureWithoutChangingTheIndex() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await GitIndexAppFixture.CreateAsync();
        var path = Path.Combine(fixture.DirectoryPath, "removed.txt");
        await File.WriteAllTextAsync(path, "external removal\n", fixture.Token);
        await using var pane = new DiffPaneViewModel(fixture.Tools);
        pane.Attach(fixture.Context);
        await pane.RefreshCommand.ExecuteAsync(null);
        pane.SelectedFile = Assert.Single(pane.Files);
        await fixture.WaitAsync(() => !pane.IsDiffLoading);
        File.Delete(path);
        await pane.IndexActions.ExecuteCommand.ExecuteAsync(null);
        Assert.Equal(InspectorText.Get("DiffIndexFailed"), pane.IndexActions.Status);
        Assert.Empty(await fixture.GitAsync("diff", "--cached", "--name-only"));
        var audits = await fixture.Audit.GetRecentAsync(fixture.Context.TaskId, 20, fixture.Token);
        var write = Assert.Single(audits, audit => audit.ToolName == "git.stage_file");
        Assert.False(write.IsSuccess);
        Assert.Equal("git_failed", write.ErrorCode);
        Assert.NotNull(write.CompletedAt);
    }, TestContext.Current.CancellationToken);
}
