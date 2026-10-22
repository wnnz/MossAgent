using Avalonia.Headless;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceComposerRouterTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task EnterAndControlEnterRouteToTheCommandSelectedByPreference() => _session.DispatchAsync(async () =>
    {
        var store = new TestComposerPreferenceStore();
        using var preferences = new ComposerPreferenceViewModel(store);
        await preferences.LoadAsync();
        var primary = 0;
        var alternate = 0;
        using var subject = new WorkspaceComposerRouter(preferences,
            new RelayCommand(() => primary++), new RelayCommand(() => alternate++), () => true);

        Assert.True(subject.SubmitOnEnter);
        subject.AlternateSubmitCommand.Execute(null);
        Assert.Equal(1, alternate);

        await preferences.SelectIndexAsync(1, TestContext.Current.CancellationToken);

        Assert.False(subject.SubmitOnEnter);
        subject.AlternateSubmitCommand.Execute(null);
        Assert.Equal(1, primary);
    }, TestContext.Current.CancellationToken);
}
