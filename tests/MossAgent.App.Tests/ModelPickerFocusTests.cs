using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelPickerFocusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task VisibleModelListFocusesSearchAndTypingDoesNotChangeDraft(bool dark, bool initialList) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        fixture.Open(initialList);
        if (!initialList) fixture.EnterModelList();

        Assert.True(fixture.Search.IsFocused);
        fixture.Window.KeyTextInput("alternative");

        Assert.Equal("alternative", fixture.Picker.SearchQuery);
        Assert.Equal("保留对话草稿", fixture.Draft.Text);
        Assert.Single(fixture.Picker.FilteredGroups);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompactListFocusesSelectedModelWhenSearchIsHidden(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark, showSearch: false);
        fixture.Open();
        fixture.EnterModelList();

        Assert.False(fixture.Search.IsEffectivelyVisible);
        var selected = Assert.Single(fixture.Menu.GetVisualDescendants().OfType<AppActionButton>(),
            button => button.Classes.Contains("model-option") && button.IsTrailingIconVisible);
        Assert.True(selected.IsFocused);
        Assert.Equal("保留对话草稿", fixture.Draft.Text);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QuickMenuStartsOnItsModelNavigationAction(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        fixture.Open();

        Assert.True(fixture.Navigation.IsFocused);
        Assert.Equal("保留对话草稿", fixture.Draft.Text);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AutomaticFocusUsesProgrammaticNavigationAndRetainsKeyboardAdorners(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        fixture.Open();

        Assert.True(fixture.Navigation.IsFocused);
        Assert.DoesNotContain(":focus-visible", fixture.Navigation.Classes);

        NavigationMethod? navigation = null;
        fixture.Search.GotFocus += (_, args) => navigation = args.NavigationMethod;
        fixture.EnterModelList();
        Assert.Equal(NavigationMethod.Unspecified, navigation);

        fixture.Picker.ShowQuickMenuCommand.Execute(null);
        fixture.Flush();
        fixture.Draft.Focus(NavigationMethod.Tab);
        fixture.Navigation.Focus(NavigationMethod.Tab);
        Assert.Contains(":focus-visible", fixture.Navigation.Classes);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ClosingMenuCancelsPendingFocusTransfer(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await ModelPickerFocusFixture.CreateAsync(dark);
        fixture.Open();
        fixture.Picker.ShowModelListCommand.Execute(null);
        fixture.Picker.IsPickerOpen = false;
        fixture.Flyout.Hide();
        fixture.Draft.Focus(NavigationMethod.Tab);
        fixture.Flush();

        Assert.True(fixture.Draft.IsFocused);
        Assert.False(fixture.Search.IsFocused);
        Assert.Equal(string.Empty, fixture.Picker.SearchQuery);
    }, TestContext.Current.CancellationToken);
}
