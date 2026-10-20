using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class HeadlessHitSceneTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LayoutFlushUpdatesTheRenderedHitSceneBeforePointerInput() => _session.DispatchAsync(() =>
    {
        var clicks = 0;
        var target = new AppButton
        {
            Content = "Move target", Width = 120, Height = 40,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(20, 20, 0, 0), Command = new RelayCommand(() => clicks++)
        };
        var window = new MainWindow { Width = 800, Height = 650,
            Content = new AppCard { Child = new Grid { Children = { target } } } };
        window.Show();
        try
        {
            ProviderUiWorkflowInput.Flush(window);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            target.Margin = new Thickness(300, 300, 0, 0);
            ProviderUiWorkflowInput.Flush(window);
            var point = target.TranslatePoint(new Point(60, 20), window)!.Value;
            var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
            Assert.True(ReferenceEquals(target, hit) || target.IsVisualAncestorOf(hit),
                $"Expected moved button at {point}; hit {hit.GetType().Name} with bounds {hit.Bounds}.");
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(1, clicks);
            return Task.CompletedTask;
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
