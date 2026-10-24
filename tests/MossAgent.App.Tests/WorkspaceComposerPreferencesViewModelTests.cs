using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerPreferencesViewModelTests
{
    [Fact]
    public async Task NullStoreDefaultsToQueueRouting()
    {
        var queue = new CountingProbeCommand();
        var steer = new CountingProbeCommand();
        using var preferences = new WorkspaceComposerPreferencesViewModel(null, queue, steer);
        Assert.True(preferences.IsLoaded);
        Assert.Equal(ComposerPreferenceText.QueueFirstHint, preferences.Hint);
        preferences.SubmitCommand.Execute(null);
        preferences.AlternateSubmitCommand.Execute(null);
        Assert.Equal(1, queue.Calls);
        Assert.Equal(1, steer.Calls);
        await preferences.StopAsync();
    }

    [Fact]
    public async Task PersistedSteerSwapsBothKeyRoutes()
    {
        var store = new ComposerPreferenceStoreProbe { Current = ComposerRunningAction.Steer };
        var queue = new CountingProbeCommand();
        var steer = new CountingProbeCommand();
        using var preferences = new WorkspaceComposerPreferencesViewModel(store, queue, steer);
        await preferences.LoadAsync();
        Assert.True(preferences.IsLoaded);
        Assert.Equal(ComposerRunningAction.Steer, preferences.Applied);
        Assert.Equal(ComposerPreferenceText.SteerFirstHint, preferences.Hint);
        preferences.SubmitCommand.Execute(null);
        preferences.AlternateSubmitCommand.Execute(null);
        Assert.Equal(1, steer.Calls);
        Assert.Equal(1, queue.Calls);
    }

    [Fact]
    public async Task DelayedLoadDoesNotSubmitUntilRoutingReady()
    {
        var gate = new TaskCompletionSource<ComposerRunningAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ComposerPreferenceStoreProbe { Read = _ => gate.Task };
        var queue = new CountingProbeCommand();
        using var preferences = new WorkspaceComposerPreferencesViewModel(store, queue, new CountingProbeCommand());
        var loading = preferences.LoadAsync();
        Assert.False(preferences.IsLoaded);
        Assert.False(preferences.SubmitCommand.CanExecute(null));
        preferences.SubmitCommand.Execute(null);
        Assert.Equal(0, queue.Calls);
        gate.SetResult(ComposerRunningAction.Queue);
        await loading;
        Assert.True(preferences.SubmitCommand.CanExecute(null));
    }

    [Fact]
    public async Task UnavailableTargetNeverFallsBackAndPropagatesSourceChanges()
    {
        var queue = new CountingProbeCommand { Enabled = false };
        var steer = new CountingProbeCommand();
        using var preferences = new WorkspaceComposerPreferencesViewModel(null, queue, steer);
        preferences.SubmitCommand.Execute(null);
        Assert.Equal(0, queue.Calls);
        Assert.Equal(0, steer.Calls);
        var raised = 0;
        preferences.SubmitCommand.CanExecuteChanged += (_, _) => raised++;
        queue.Enabled = true;
        queue.RaiseCanExecuteChanged();
        Assert.True(preferences.SubmitCommand.CanExecute(null));
        Assert.True(raised > 0);
    }

    [Fact]
    public async Task SaveAppliesNewRouteOnlyOnSuccessAndKeepsDraftOnFailure()
    {
        var store = new ComposerPreferenceStoreProbe();
        var queue = new CountingProbeCommand();
        var steer = new CountingProbeCommand();
        using var preferences = new WorkspaceComposerPreferencesViewModel(store, queue, steer);
        await preferences.LoadAsync();
        preferences.SelectedOption = preferences.Options[1];
        Assert.True(preferences.SaveCommand.CanExecute(null));
        preferences.SubmitCommand.Execute(null);
        Assert.Equal(1, queue.Calls);
        Assert.Equal(0, steer.Calls);
        await preferences.SaveCommand.ExecuteAsync(null);
        Assert.Equal(ComposerRunningAction.Steer, preferences.Applied);
        preferences.SubmitCommand.Execute(null);
        Assert.Equal(1, steer.Calls);

        store.Write = (_, _) => Task.FromException(new InvalidOperationException("synthetic-save-secret"));
        preferences.SelectedOption = preferences.Options[0];
        await preferences.SaveCommand.ExecuteAsync(null);
        Assert.Equal(ComposerRunningAction.Steer, preferences.Applied);
        Assert.Equal(preferences.Options[0], preferences.SelectedOption);
        Assert.True(preferences.HasError);
        Assert.DoesNotContain("synthetic-save-secret", preferences.Error);
    }

    [Fact]
    public async Task LoadFailureShowsDefaultWithWarningAndReloadRecovers()
    {
        var store = new ComposerPreferenceStoreProbe
        {
            Read = _ => Task.FromException<ComposerRunningAction>(new IOException("synthetic-load-secret"))
        };
        using var preferences = new WorkspaceComposerPreferencesViewModel(store, new CountingProbeCommand(), new CountingProbeCommand());
        await preferences.LoadAsync();
        Assert.True(preferences.IsLoaded);
        Assert.True(preferences.HasError);
        Assert.DoesNotContain("synthetic-load-secret", preferences.Error);
        Assert.Equal(ComposerRunningAction.Queue, preferences.Applied);
        Assert.False(preferences.SaveCommand.CanExecute(null));

        store.Read = _ => Task.FromResult(ComposerRunningAction.Steer);
        await preferences.ReloadCommand.ExecuteAsync(null);
        Assert.False(preferences.HasError);
        Assert.Equal(ComposerRunningAction.Steer, preferences.Applied);
    }
}
