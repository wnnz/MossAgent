using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
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
public sealed class ApprovalGestureIdentityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public Task ReleasingAnOldGestureCannotDecideTheNextRequest(bool dark, bool pointer, bool approve) =>
        _session.DispatchAsync(async () =>
        {
            var approval = new ApprovalViewModel();
            using var firstCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var secondCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var first = Request(approval, "first", firstCancellation.Token);
            var firstId = approval.CurrentRequestId;
            var second = Request(approval, "second", secondCancellation.Token);
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
                var button = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(),
                    candidate => ReferenceEquals(candidate.Command, approve ? approval.ApproveCommand : approval.DenyCommand));
                Assert.Equal(firstId, button.CommandParameter);
                Assert.True(button.Focus());
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                if (pointer) window.MouseDown(point, MouseButton.Left);
                else window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Assert.False(first.IsCompleted);
                firstCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
                Flush(window);
                Assert.NotEqual(firstId, approval.CurrentRequestId);
                Assert.Equal(approval.CurrentRequestId, button.CommandParameter);
                if (pointer) window.MouseUp(point, MouseButton.Left);
                else window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Flush(window);
                Assert.False(second.IsCompleted, "A gesture started for a cancelled request must not decide its successor.");
                Assert.True(approval.IsPending);
                Assert.Equal(1, approval.PendingCount);
                Assert.False(button.IsPressed);
                if (pointer)
                {
                    window.MouseDown(point, MouseButton.Left);
                    window.MouseUp(point, MouseButton.Left);
                }
                else
                {
                    window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                    window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                }
                Assert.Equal(approve, await second);
            }
            finally
            {
                firstCancellation.Cancel();
                secondCancellation.Cancel();
                try { await second; }
                catch (OperationCanceledException) { }
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private static Task<bool> Request(ApprovalViewModel approval, string id, CancellationToken token)
    {
        var descriptor = new ToolDescriptor("filesystem.write_file", "synthetic gesture fixture", "{}",
            ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        return approval.RequestAsync(descriptor, new ToolRequest(id, descriptor.Name,
            JsonSerializer.SerializeToElement(new { path = @"C:\fixture\" + id + ".txt", content = "synthetic" })), token).AsTask();
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
