using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarSearchComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardSearch_BindsBothWaysAndReturnsFocusWithoutChangingTheDraft(bool dark) => WithSearch(dark,
        (window, search, composer, state) =>
        {
            composer.Focus();
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, KeyModifiers = KeyModifiers.Control };
            composer.RaiseEvent(key);
            Dispatcher.UIThread.RunJobs();
            Assert.True(key.Handled);
            Assert.True(search.IsSearchOpen);
            var input = search.FindControl<AppInput>("SearchInput")!;
            Assert.True(input.IsFocused);
            input.Text = "no match";
            Assert.Equal("no match", state.Query);
            Assert.True(search.ShowEmptyState);
            state.Query = "external query";
            Assert.Equal("external query", input.Text);
            var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
            input.RaiseEvent(escape);
            Dispatcher.UIThread.RunJobs();
            Assert.True(escape.Handled);
            Assert.False(search.IsSearchOpen);
            Assert.True(composer.IsFocused);
            Assert.Equal("external query", search.Query);
            Assert.Equal("保留的草稿", composer.Text);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SharedPopup_UsesCompactAlignedIconsAndAnUnframedSearchField(bool dark) => WithSearch(dark,
        (window, search, composer, state) =>
        {
            state.Query = string.Concat(Enumerable.Repeat("长搜索词", 20));
            Assert.True(search.OpenSearch());
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var input = search.FindControl<AppInput>("SearchInput")!;
            var clear = search.FindControl<AppIconButton>("ClearSearchButton")!;
            var surface = search.FindControl<AppCard>("SearchSurface")!;
            Assert.Equal(270, surface.Bounds.Width);
            Assert.Equal(new Thickness(0), input.BorderThickness);
            Assert.True(input.Bounds.Width > 100);
            Assert.True(clear.IsVisible);
            var icon = Assert.Single(clear.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(12, icon.Bounds.Width);
            Assert.Equal(Center(input, surface).Y, Center(clear, surface).Y, precision: 6);
            Assert.Equal(Center(clear, surface).Y, Center(icon, surface).Y, precision: 6);
            var trigger = search.FindControl<AppIconButton>("SearchTrigger")!;
            Assert.Contains("filter-active", trigger.Classes);
            Assert.Contains(state.Query.Trim(), trigger.Label!);
            Assert.NotEqual(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(trigger.Background).Color);
        });

    [Fact]
    public Task Clear_UsesTheExistingCommandAndRetainsSearchFocusAndEmptyFieldLayout() => WithSearch(false,
        (window, search, composer, state) =>
        {
            state.Query = "no match";
            search.OpenSearch();
            Dispatcher.UIThread.RunJobs();
            var clear = search.FindControl<AppIconButton>("ClearSearchButton")!;
            clear.Focus();
            clear.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            clear.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(string.Empty, state.Query);
            Assert.False(search.ShowEmptyState);
            Assert.False(clear.IsVisible);
            Assert.True(search.FindControl<AppInput>("SearchInput")!.IsFocused);
            Assert.True(search.IsSearchOpen);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingOrDisabledClearCommand_DoesNotClearTheQuery(bool missing) => WithSearch(false,
        (window, search, composer, state) =>
        {
            var count = 0;
            search.Query = "query";
            search.ClearCommand = missing ? null : new RelayCommand(() => count++, () => false);
            search.OpenSearch();
            Dispatcher.UIThread.RunJobs();
            var clear = search.FindControl<AppIconButton>("ClearSearchButton")!;
            Assert.False(clear.IsEffectivelyEnabled);
            clear.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            clear.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            Assert.Equal(0, count);
            Assert.Equal("query", search.Query);
        });

    [Theory]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("detached")]
    [InlineData("owner")]
    public Task LifetimeChange_ClosesPopupAndRejectsDeferredFocus(string reason) => WithSearch(false,
        (window, search, composer, state) =>
        {
            composer.Focus();
            search.OpenSearch();
            var grid = Assert.IsType<Grid>(window.Content);
            if (reason == "hidden") grid.IsVisible = false;
            if (reason == "disabled") grid.IsEnabled = false;
            if (reason == "detached") grid.Children.Remove(search);
            if (reason == "owner") search.DataContext = new object();
            Dispatcher.UIThread.RunJobs();
            Assert.False(search.IsSearchOpen);
            if (reason != "owner") Assert.False(search.OpenSearch());
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, KeyModifiers = KeyModifiers.Control };
            if (reason != "owner")
            {
                window.RaiseEvent(key);
                Assert.False(key.Handled);
            }
        });

    [Fact]
    public Task ImmediateClose_DoesNotStealFocusAndOtherShortcutsAreNotIntercepted() => WithSearch(false,
        (window, search, composer, state) =>
        {
            search.OpenSearch();
            search.CloseSearch();
            composer.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.True(composer.IsFocused);
            Assert.False(search.IsSearchOpen);
            var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift };
            composer.RaiseEvent(key);
            Assert.False(key.Handled);
            Assert.False(search.IsSearchOpen);
        });

    private Task WithSearch(bool dark, Action<AppWindow, AppSidebarSearch, AppInput, WorkspaceSidebarSearchViewModel> action) =>
        _session.Dispatch(() =>
        {
            using var state = new WorkspaceSidebarSearchViewModel(SidebarSearchViewModelTests.Catalog());
            var search = new AppSidebarSearch();
            search.Bind(AppSidebarSearch.QueryProperty, new Binding(nameof(state.Query)) { Source = state, Mode = BindingMode.TwoWay });
            search.Bind(AppSidebarSearch.HasMatchesProperty, new Binding(nameof(state.HasMatches)) { Source = state });
            search.ClearCommand = state.ClearCommand;
            var composer = new AppInput { Text = "保留的草稿" };
            Grid.SetRow(composer, 1);
            var window = new AppWindow
            {
                Width = 320,
                Height = 180,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { search, composer } }
            };
            window.Show();
            try { window.UpdateLayout(); action(window, search, composer, state); }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static Point Center(Control control, Control relative) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), relative)!.Value;
}
