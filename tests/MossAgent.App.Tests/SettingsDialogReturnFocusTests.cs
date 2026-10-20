using Avalonia.Headless;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsDialogReturnFocusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ClosingRestoresTheTriggerAfterItsCommandIsReenabled() => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        var open = new RelayCommand(() => fixture.Dialog.IsOpen = true, () => !fixture.Dialog.IsOpen);
        fixture.Navigation.Command = open;
        fixture.Open();
        open.NotifyCanExecuteChanged();
        Assert.False(fixture.Navigation.IsEffectivelyEnabled);
        fixture.Dialog.CloseCommand = new RelayCommand(() =>
        {
            fixture.Dialog.IsOpen = false;
            open.NotifyCanExecuteChanged();
        });

        fixture.PressKey(Key.Escape);

        Assert.False(fixture.Dialog.IsOpen);
        Assert.True(fixture.Navigation.IsEffectivelyEnabled);
        Assert.True(fixture.Navigation.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("disabled")]
    [InlineData("hidden")]
    [InlineData("detached")]
    [InlineData("reopened")]
    [InlineData("new-focus")]
    public Task DeferredReturnDoesNotOverrideNewerOrUnavailableFocus(string state) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        var other = new AppButton { Content = "其他操作" };
        fixture.Page.Children.Insert(0, other);
        fixture.Open();
        fixture.Navigation.IsEnabled = false;
        fixture.Dialog.IsOpen = false;
        if (state != "disabled") fixture.Navigation.IsEnabled = true;
        if (state == "hidden") fixture.Navigation.IsVisible = false;
        if (state == "detached") fixture.Page.Children.Remove(fixture.Dialog);
        if (state == "reopened") fixture.Dialog.IsOpen = true;
        if (state == "new-focus") Assert.True(other.Focus());

        fixture.Flush();

        Assert.False(fixture.Navigation.IsFocused);
        if (state == "reopened") Assert.True(fixture.First.IsFocused);
        if (state == "new-focus") Assert.True(other.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
