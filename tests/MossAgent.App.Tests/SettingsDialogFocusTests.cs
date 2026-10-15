using Avalonia.Headless;
using Avalonia.Input;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsDialogFocusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReturningToPageRestoresDialogFocusAndEscapeClosing(bool dark) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture(dark);
        fixture.Open();
        Assert.True(fixture.First.IsFocused);
        fixture.HidePage();
        Assert.True(fixture.Navigation.IsFocused);
        Assert.True(fixture.Dialog.IsOpen);

        fixture.ShowPage();

        Assert.True(fixture.First.IsFocused);
        Assert.Equal("模型 ID 草稿", fixture.First.Text);
        fixture.PressKey(Key.Escape);
        Assert.False(fixture.Dialog.IsOpen);
        Assert.True(fixture.Navigation.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task ReturningToPageRespectsTheLastFieldsSelectionPolicy(bool dark, bool clearSelection) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture(dark);
        fixture.Second.ClearSelectionOnLostFocus = clearSelection;
        fixture.Open();
        fixture.Second.Focus();
        fixture.Second.SelectionStart = 1;
        fixture.Second.SelectionEnd = 3;
        fixture.HidePage();

        fixture.ShowPage();

        Assert.True(fixture.Second.IsFocused);
        Assert.Equal("显示名称草稿", fixture.Second.Text);
        Assert.Equal(1, fixture.Second.SelectionStart);
        Assert.Equal(clearSelection ? 1 : 3, fixture.Second.SelectionEnd);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task UnavailableRememberedFieldFallsBackToAnAvailableInput(bool disabled) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        fixture.Open();
        fixture.Second.Focus();
        fixture.HidePage();
        if (disabled) fixture.Second.IsEnabled = false;
        else fixture.Second.IsVisible = false;

        fixture.ShowPage();

        Assert.True(fixture.First.IsFocused);
        Assert.False(fixture.Second.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AnUnavailableAncestorRejectsPendingFocus(bool disabled) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        fixture.Navigation.Focus();
        fixture.Dialog.IsOpen = true;
        if (disabled) fixture.Page.IsEnabled = false;
        else fixture.Page.IsVisible = false;
        fixture.Navigation.Focus();

        fixture.Flush();

        Assert.True(fixture.Navigation.IsFocused);
        Assert.False(fixture.First.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ClosingOrDetachingRejectsPendingFocus(bool detach) => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        fixture.Dialog.IsOpen = true;
        if (detach) fixture.Page.Children.Remove(fixture.Dialog);
        else fixture.Dialog.IsOpen = false;
        fixture.Navigation.Focus();

        fixture.Flush();

        Assert.True(fixture.Navigation.IsFocused);
        Assert.False(fixture.First.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReattachingAnOpenDialogObtainsFocusWithoutLosingItsDraft() => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        fixture.Open();
        fixture.Page.Children.Remove(fixture.Dialog);
        fixture.Navigation.Focus();
        fixture.Flush();

        fixture.Page.Children.Add(fixture.Dialog);
        fixture.Flush();

        Assert.True(fixture.First.IsFocused);
        Assert.Equal("模型 ID 草稿", fixture.First.Text);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpeningDoesNotOverrideFocusAlreadyMovedInsideTheDialog() => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        fixture.Dialog.IsOpen = true;
        fixture.Window.UpdateLayout();
        fixture.Second.Focus();

        fixture.Flush();

        Assert.True(fixture.Second.IsFocused);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AClosedDialogDoesNotTakeFocusWhenItsPageReturns() => _session.DispatchAsync(() =>
    {
        using var fixture = new SettingsDialogFocusFixture();
        fixture.Open();
        fixture.Dialog.IsOpen = false;
        fixture.HidePage();

        fixture.ShowPage();

        Assert.True(fixture.Navigation.IsFocused);
        Assert.False(fixture.Dialog.IsVisible);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);
}
