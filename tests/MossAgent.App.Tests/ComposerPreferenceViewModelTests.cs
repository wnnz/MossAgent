using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerPreferenceViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LoadAndSelectionPersistTheSavedMode() => _session.DispatchAsync(async () =>
    {
        var store = new TestComposerPreferenceStore { Mode = ComposerSendMode.ControlEnterSends };
        using var subject = new ComposerPreferenceViewModel(store);

        await subject.LoadAsync();
        Assert.True(subject.IsReady);
        Assert.Equal(1, subject.SelectedIndex);
        Assert.False(subject.SubmitOnEnter);
        Assert.False(subject.HasError);

        await subject.SelectIndexAsync(0, TestContext.Current.CancellationToken);
        Assert.Equal(ComposerSendMode.EnterSends, store.Mode);
        Assert.True(subject.SubmitOnEnter);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SaveFailureRestoresTheLastModeAndShowsAnActionableError() => _session.DispatchAsync(async () =>
    {
        var store = new TestComposerPreferenceStore { ThrowOnSave = true };
        using var subject = new ComposerPreferenceViewModel(store);
        await subject.LoadAsync();

        await subject.SelectIndexAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(0, subject.SelectedIndex);
        Assert.Equal(ComposerInputText.PreferenceSaveFailed, subject.Error);
        Assert.True(subject.HasError);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CorruptConfigurationFallsBackWithoutOverwritingAndRemainsUsable() => _session.DispatchAsync(async () =>
    {
        var store = new TestComposerPreferenceStore { ThrowOnLoad = true };
        using var subject = new ComposerPreferenceViewModel(store);
        await subject.LoadAsync();

        Assert.True(subject.IsReady);
        Assert.True(subject.SubmitOnEnter);
        Assert.Equal(ComposerInputText.PreferenceLoadFailed, subject.Error);
        Assert.Equal(0, store.SaveCount);
    }, TestContext.Current.CancellationToken);
}
