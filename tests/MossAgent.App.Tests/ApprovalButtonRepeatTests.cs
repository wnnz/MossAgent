using System.Text.Json;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ApprovalButtonRepeatTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task HeldEnterDoesNotDecideTheNextRequest(bool dark, bool approve) => WithQueue(dark, approve, async (window, first, second) =>
    {
        Down(window, Key.Enter);
        Assert.Equal(approve, await first);
        Flush(window);
        for (var repeat = 0; repeat < 8; repeat++) Down(window, Key.Enter);
        Assert.False(second.IsCompleted);
        Up(window, Key.Enter);
        Down(window, Key.Enter);
        Up(window, Key.Enter);
        Assert.Equal(approve, await second);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OverlappingKeysRemainBoundUntilBothAreReleased(bool dark) => WithQueue(dark, true, async (window, first, second) =>
    {
        Down(window, Key.Space);
        Down(window, Key.Enter);
        Assert.True(await first);
        Flush(window);
        Up(window, Key.Enter);
        Down(window, Key.Enter);
        Assert.False(second.IsCompleted);
        Up(window, Key.Enter);
        Up(window, Key.Space);
        Assert.False(second.IsCompleted);
        Down(window, Key.Space);
        Up(window, Key.Space);
        Assert.True(await second);
    });

    private Task WithQueue(bool dark, bool approve, Func<AppWindow, Task<bool>, Task<bool>, Task> action) =>
        _session.DispatchAsync(async () =>
        {
            var approval = new ApprovalViewModel();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.CancelAfter(TimeSpan.FromSeconds(10));
            var descriptor = new ToolDescriptor("filesystem.write_file", "synthetic keyboard fixture", "{}",
                ToolRiskLevel.Mutation, ToolCapability.FileWrite);
            var request = new ToolRequest("fixture", descriptor.Name, JsonSerializer.SerializeToElement(new { path = "fixture.txt" }));
            var first = approval.RequestAsync(descriptor, request, cancellation.Token).AsTask();
            var second = approval.RequestAsync(descriptor, request, cancellation.Token).AsTask();
            var view = new ToolApprovalView { DataContext = approval };
            var window = new AppWindow
            {
                Width = 980,
                Height = 700,
                Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                Flush(window);
                var button = Assert.Single(view.GetVisualDescendants().OfType<AppApprovalButton>(),
                    candidate => ReferenceEquals(candidate.Command, approve ? approval.ApproveCommand : approval.DenyCommand));
                Assert.True(button.Focus());
                await action(window, first, second);
                Assert.False(approval.IsPending);
            }
            finally
            {
                cancellation.Cancel();
                try { await Task.WhenAll(first, second); }
                catch (OperationCanceledException) { }
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private static void Down(AppWindow window, Key key) => window.KeyPress(key, RawInputModifiers.None,
        key == Key.Enter ? PhysicalKey.Enter : PhysicalKey.Space, key == Key.Enter ? "\r" : " ");

    private static void Up(AppWindow window, Key key) => window.KeyRelease(key, RawInputModifiers.None,
        key == Key.Enter ? PhysicalKey.Enter : PhysicalKey.Space, key == Key.Enter ? "\r" : " ");

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
