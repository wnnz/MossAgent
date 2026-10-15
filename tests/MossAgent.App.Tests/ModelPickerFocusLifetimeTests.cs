using Avalonia.Headless;
using Avalonia.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelPickerFocusLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReplacementPickerRejectsPendingAndOldOwnerFocus(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        using var replacement = await ModelPickerFocusFixture.CreatePickerAsync();
        fixture.Open();
        fixture.Picker.ShowModelListCommand.Execute(null);
        fixture.Menu.DataContext = replacement;
        fixture.Draft.Focus(NavigationMethod.Tab);
        fixture.Flush();
        Assert.True(fixture.Draft.IsFocused);

        fixture.Picker.ShowQuickMenuCommand.Execute(null);
        fixture.Picker.ShowModelListCommand.Execute(null);
        fixture.Flush();
        Assert.True(fixture.Draft.IsFocused);

        replacement.ShowModelListCommand.Execute(null);
        replacement.IsPickerOpen = true;
        fixture.Flush();
        Assert.True(fixture.Search.IsFocused);
        fixture.Window.KeyTextInput("alternative");
        Assert.Equal("alternative", replacement.SearchQuery);
        Assert.Empty(fixture.Picker.SearchQuery);
        Assert.Equal("保留对话草稿", fixture.Draft.Text);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DetachedMenuIgnoresOldNotificationsAndReattachesToCurrentMode(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        fixture.Open();
        var component = Assert.IsType<AppModelPickerMenu>(fixture.Menu.Content);
        fixture.Picker.ShowModelListCommand.Execute(null);
        fixture.Menu.Content = null;
        fixture.Draft.Focus(NavigationMethod.Tab);
        fixture.Flush();
        Assert.True(fixture.Draft.IsFocused);

        fixture.Picker.ShowQuickMenuCommand.Execute(null);
        fixture.Picker.ShowModelListCommand.Execute(null);
        fixture.Flush();
        Assert.True(fixture.Draft.IsFocused);

        fixture.Menu.Content = component;
        fixture.Flush();
        Assert.True(fixture.Search.IsFocused);
        Assert.Equal("保留对话草稿", fixture.Draft.Text);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public Task UnavailableMenuOrHostCannotTakeDraftFocus(bool dark, bool disabled, bool host) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        fixture.Open();
        var component = Assert.IsType<AppModelPickerMenu>(fixture.Menu.Content);
        var target = host ? fixture.Menu : (AppUserControl)component;
        if (disabled) target.IsEnabled = false;
        else target.IsVisible = false;
        fixture.Picker.ShowModelListCommand.Execute(null);
        fixture.Draft.Focus(NavigationMethod.Tab);
        fixture.Flush();

        Assert.True(fixture.Draft.IsFocused);
        Assert.False(fixture.Search.IsFocused);
        fixture.Window.KeyTextInput("仍在草稿");
        Assert.Empty(fixture.Picker.SearchQuery);
        Assert.Contains("仍在草稿", fixture.Draft.Text, StringComparison.Ordinal);
    }, TestContext.Current.CancellationToken);
}
