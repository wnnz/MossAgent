using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarComposeFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(false, 1280, 820)]
    [InlineData(false, 1920, 1032)]
    [InlineData(true, 980, 640)]
    [InlineData(true, 1280, 820)]
    [InlineData(true, 1920, 1032)]
    public Task ActualSidebar_NewConversationKeepsMeasuredGeometryAndCommand(bool dark, double width, double height) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var window = new MainWindow
            {
                DataContext = fixture.Main,
                Width = width,
                Height = height,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var action = Assert.Single(window.GetVisualDescendants().OfType<AppActionButton>(),
                    button => button.Classes.Contains("sidebar-action-button"));
                var label = Assert.Single(action.GetVisualDescendants().OfType<AppIconLabel>());
                var icon = Assert.Single(label.GetVisualDescendants().OfType<AppIcon>());
                var text = Assert.Single(label.GetVisualDescendants().OfType<AppText>());
                TestContext.Current.TestOutputHelper!.WriteLine($"action={Origin(action, window)} size={action.Bounds.Size} margin={action.Margin}; "
                    + $"icon={Origin(icon, window)} size={icon.Bounds.Size} geometry={icon.Data!.Bounds}; "
                    + $"label={Origin(text, window)} size={text.Bounds.Size} spacing={label.Spacing}");
                Assert.Same(fixture.Workspace.Catalog.NewTaskCommand, action.Command);
                Assert.Equal(new Thickness(0, 2, 0, 1), action.Margin);
                Assert.Equal(new Thickness(10, 2), action.Padding);
                Assert.Equal(30, action.Bounds.Height);
                Assert.Equal(new Size(11, 11), icon.Bounds.Size);
                Assert.Equal(1, icon.StrokeThickness);
                Assert.Equal(11, label.Spacing);
                Assert.Equal(new Matrix(1, 0, 0, 1, 0.5, -1), icon.RenderTransform!.Value);
                Assert.Equal(92, Origin(text, window).X);
                Assert.Equal(112, Origin(label, window).Y + label.Bounds.Height / 2);
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task GeneralActionButtons_RetainDefaultIconAndSpacing(bool dark) =>
        _session.Dispatch(() =>
        {
            var action = new AppActionButton { Label = "other", IconData = Geometry.Parse("M 2 2 L 22 22") };
            var window = new AppWindow
            {
                Width = 300,
                Height = 100,
                Content = action,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var label = Assert.Single(action.GetVisualDescendants().OfType<AppIconLabel>());
                var icon = Assert.Single(label.GetVisualDescendants().OfType<AppIcon>());
                Assert.Equal(new Size(14, 14), icon.Bounds.Size);
                Assert.Equal(1.2, icon.StrokeThickness);
                Assert.Equal(9, label.Spacing);
                Assert.Null(icon.RenderTransform);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardActivation_StillObeysCommandAvailability(bool dark) =>
        _session.Dispatch(() =>
        {
            var allowed = true;
            var calls = 0;
            var command = new RelayCommand(() => calls++, () => allowed);
            var action = new AppActionButton
            {
                Classes = { "ghost", "sidebar-action-button", "align-leading" },
                Label = "new conversation",
                Command = command
            };
            var window = new AppWindow
            {
                Width = 300,
                Height = 100,
                Content = action,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.True(action.Focus(NavigationMethod.Tab));
                action.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                action.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
                Assert.Equal(1, calls);
                allowed = false;
                command.NotifyCanExecuteChanged();
                Dispatcher.UIThread.RunJobs();
                Assert.False(action.IsEffectivelyEnabled);
                Assert.Equal(1, calls);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static Point Origin(Control control, Control relative) => control.TranslatePoint(new Point(), relative)!.Value;
}
