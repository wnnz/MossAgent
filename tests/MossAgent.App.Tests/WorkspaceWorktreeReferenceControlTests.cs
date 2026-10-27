using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Git;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceWorktreeReferenceControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526)]
    [InlineData(true, 526)]
    [InlineData(false, 1000)]
    [InlineData(true, 1000)]
    public Task SharedInputAcceptsKeyboardPreservesValueOnReopenAndLocks(bool dark, int width) => _session.Dispatch(() =>
    {
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(new ProjectProfile(Guid.NewGuid(), "project", "root", ["root"], DateTimeOffset.UtcNow), null);
        var view = new WorkspaceTaskEnvironmentView { DataContext = environment,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
        var window = new AppWindow { Width = width, Height = 400, Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = view.FindControl<AppActionButton>("EnvironmentButton")!;
            var flyout = Assert.IsType<AppFlyout>(button.Flyout);
            flyout.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            var card = Assert.IsType<AppCard>(flyout.Content);
            var input = Assert.Single(card.GetVisualDescendants().OfType<AppInput>(),
                item => item.Name == "BaseReferenceInput");
            Assert.True(input.IsEffectivelyVisible);
            Assert.True(input.Focus(NavigationMethod.Tab));
            window.KeyTextInput("selected-base");
            Assert.Equal("selected-base", environment.BaseReference);
            Assert.True(flyout.IsOpen);
            Assert.Equal(environment.BaseReferenceLabel, AutomationProperties.GetName(input));
            Assert.Equal(512, input.MaxLength);
            window.UpdateLayout();
            Assert.True(input.Bounds.Width > 0 && input.Bounds.Width <= card.Bounds.Width);
            using var image = window.CaptureRenderedFrame();
            var directory = Path.Combine(AppContext.BaseDirectory, "worktree-reference-evidence");
            Directory.CreateDirectory(directory);
            Assert.NotNull(image);
            image.Save(Path.Combine(directory, $"reference-{dark}-{width}.png"), PngBitmapEncoderOptions.Default);
            var actions = card.GetVisualDescendants().OfType<AppActionButton>().ToArray();
            Activate(window, Assert.Single(actions, action => ReferenceEquals(action.Command, environment.UseWorktreeCommand)));
            Assert.True(environment.UseWorktree);
            Assert.False(flyout.IsOpen);
            flyout.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("selected-base", input.Text);
            Activate(window, Assert.Single(actions, action => ReferenceEquals(action.Command, environment.UseProjectDirectoryCommand)));
            Assert.False(environment.UseWorktree);
            Assert.False(flyout.IsOpen);
            flyout.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("selected-base", input.Text);
            environment.SetLocked(true);
            Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            Assert.False(button.IsEnabled);
            Assert.Equal("selected-base", environment.BaseReference);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DiscoveredReferencesRenderAndSelectBaseReference() => _session.DispatchAsync(async () =>
    {
        var environment = new WorkspaceTaskEnvironmentViewModel(new StaticWorktreeReferenceDiscovery());
        environment.SetContext(new ProjectProfile(Guid.NewGuid(), "project", "root", ["root"], DateTimeOffset.UtcNow), null);
        var view = new WorkspaceTaskEnvironmentView { DataContext = environment,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
        var window = new AppWindow { Width = 526, Height = 480, Content = view, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = view.FindControl<AppActionButton>("EnvironmentButton")!;
            var flyout = Assert.IsType<AppFlyout>(button.Flyout);
            flyout.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            await environment.References.RefreshAsync();
            Dispatcher.UIThread.RunJobs();
            var card = Assert.IsType<AppCard>(flyout.Content);
            var list = Assert.Single(card.GetVisualDescendants().OfType<AppListBox>());
            Assert.True(list.IsEffectivelyVisible);
            Assert.Equal(2, list.ItemCount);
            var search = Assert.Single(card.GetVisualDescendants().OfType<AppInput>(),
                input => input.Name == "ReferenceSearchInput");
            Assert.Equal(environment.References.Label, AutomationProperties.GetName(search));
            environment.References.SelectedItem = environment.References.Items[1];
            Assert.Equal("refs/heads/ref-1", environment.BaseReference);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Activate(AppWindow window, AppActionButton action)
    {
        Assert.True(action.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Dispatcher.UIThread.RunJobs();
    }
}
