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
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SidebarTaskTextTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task FittingTitles_RemainOpaqueAndUseMeasuredTaskSize(bool dark, bool projectTask) =>
        WithRow(dark, projectTask, (window, row, label, outside) =>
        {
            row.Title = "排查截图工具黑屏问题";
            Refresh(window);
            Assert.True(label.TextLayout.Width < label.Bounds.Width);
            Assert.Null(label.OpacityMask);
            Assert.Equal(14, label.FontSize);
            Assert.Equal(18, label.LineHeight);
            Assert.Equal(Color.Parse(dark ? "#E6E6E6" : "#3B3E40"),
                Assert.IsType<SolidColorBrush>(label.Foreground).Color);
            Assert.Equal(30, row.Bounds.Height);
            Assert.Equal(projectTask ? 32 : 8, label.TranslatePoint(default, row)!.Value.X, precision: 3);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task TextAndWidthChanges_OnlyFadeActualOverflow(bool dark, bool projectTask) =>
        WithRow(dark, projectTask, (window, row, label, outside) =>
        {
            row.Title = string.Concat(Enumerable.Repeat("侧边栏长标题", 30));
            Refresh(window);
            Assert.True(label.TextLayout.Width > label.Bounds.Width);
            Assert.NotNull(label.OpacityMask);
            Assert.True(label.ClipToBounds);
            Assert.Equal(TextTrimming.None, label.TextTrimming);
            row.Title = "排查截图工具黑屏问题";
            Refresh(window);
            Assert.Null(label.OpacityMask);
            window.Width = 148;
            Refresh(window);
            Assert.True(label.TextLayout.Width > label.Bounds.Width);
            Assert.NotNull(label.OpacityMask);
            window.Width = 264;
            Refresh(window);
            Assert.Null(label.OpacityMask);
            row.Title = string.Empty;
            Refresh(window);
            Assert.Null(label.OpacityMask);
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActionAndRunningReservations_RefreshOverflowWithoutChangingSelection(bool dark, bool projectTask) =>
        WithRow(dark, projectTask, (window, row, label, outside) =>
        {
            row.Title = new string('中', projectTask ? 15 : 16);
            Refresh(window);
            Assert.True(label.TextLayout.Width < label.Bounds.Width);
            Assert.Null(label.OpacityMask);
            var options = row.FindControl<AppIconButton>("TaskOptionsButton")!;
            Assert.True(options.Focus(NavigationMethod.Tab));
            Refresh(window);
            Assert.True(label.TextLayout.Width > label.Bounds.Width);
            Assert.NotNull(label.OpacityMask);
            Assert.True(outside.Focus());
            Refresh(window);
            Assert.Null(label.OpacityMask);
            row.IsRunning = true;
            Refresh(window);
            Assert.NotNull(label.OpacityMask);
            row.IsRunning = false;
            Refresh(window);
            Assert.Null(label.OpacityMask);
            row.IsSelected = true;
            Refresh(window);
            Assert.Null(label.OpacityMask);
            Assert.True(row.IsSelected);
        });

    [Theory]
    [InlineData(false, "fonts:Inter#Inter")]
    [InlineData(false, "Segoe UI")]
    [InlineData(true, "fonts:Inter#Inter")]
    [InlineData(true, "Segoe UI")]
    public Task SharedText_KeepsInheritedFontAndDoesNotFadeOtherRoles(bool dark, string family) =>
        _session.Dispatch(() =>
        {
            var ordinary = new AppText { Text = new string('中', 100) };
            var section = new AppText { Text = "项目", Classes = { "sidebar-section-heading" } };
            var window = new AppWindow
            {
                Width = 100,
                Height = 100,
                FontFamily = new FontFamily(family),
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
                Content = new StackPanel { Children = { ordinary, section } }
            };
            window.Show();
            try
            {
                Refresh(window);
                Assert.Equal(window.FontFamily, ordinary.FontFamily);
                Assert.Equal(window.FontFamily, section.FontFamily);
                Assert.Null(ordinary.OpacityMask);
                Assert.Equal(13, ordinary.FontSize);
                Assert.Equal(Color.Parse(dark ? "#A0A0A0" : "#6B7075"),
                    Assert.IsType<SolidColorBrush>(section.Foreground).Color);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OverflowOptOutAndReattachment_DoNotKeepStaleMasks(bool dark) =>
        WithRow(dark, true, (window, row, label, outside) =>
        {
            row.Title = new string('中', 100);
            Refresh(window);
            Assert.True(label.FadeOverflow);
            Assert.NotNull(label.OpacityMask);
            label.FadeOverflow = false;
            Refresh(window);
            Assert.Null(label.OpacityMask);
            label.ClearValue(AppText.FadeOverflowProperty);
            Refresh(window);
            Assert.NotNull(label.OpacityMask);
            var container = Assert.IsType<Grid>(window.Content);
            container.Children.Remove(row);
            Assert.Null(label.OpacityMask);
            row.Title = "短标题";
            container.Children.Insert(0, row);
            Refresh(window);
            Assert.Null(label.OpacityMask);
            row.Title = new string('中', 100);
            Refresh(window);
            Assert.NotNull(label.OpacityMask);
        });

    [Theory]
    [InlineData(false, "fonts:Inter#Inter")]
    [InlineData(false, "Segoe UI")]
    [InlineData(true, "fonts:Inter#Inter")]
    [InlineData(true, "Segoe UI")]
    public Task FontChanges_KeepPreferenceAndReevaluateOverflow(bool dark, string family) =>
        WithRow(dark, true, (window, row, label, outside) =>
        {
            window.FontFamily = new FontFamily(family);
            row.Title = "排查截图工具黑屏问题";
            Refresh(window);
            Assert.Equal(window.FontFamily, label.FontFamily);
            Assert.Null(label.OpacityMask);
            label.FontSize = 28;
            Refresh(window);
            Assert.NotNull(label.OpacityMask);
            label.ClearValue(TextBlock.FontSizeProperty);
            Refresh(window);
            Assert.Equal(14, label.FontSize);
            Assert.Null(label.OpacityMask);
        });

    private Task WithRow(bool dark, bool projectTask,
        Action<AppWindow, AppTaskNavigationRow, AppText, AppButton> action) => _session.Dispatch(() =>
    {
        var row = new AppTaskNavigationRow
        {
            IsProjectTask = projectTask,
            SelectCommand = new RelayCommand(() => { }),
            CommandParameter = new object()
        };
        var outside = new AppButton { Content = "outside" };
        Grid.SetRow(outside, 1);
        var window = new AppWindow
        {
            Width = 264,
            Height = 120,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { row, outside } }
        };
        window.Show();
        try
        {
            outside.Focus();
            window.MouseMove(new Point(260, 115));
            Refresh(window);
            var label = Assert.Single(row.FindControl<AppButton>("SelectTaskButton")!
                .GetVisualDescendants().OfType<AppText>());
            action(window, row, label, outside);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Refresh(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
