using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsItemRowLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320)]
    [InlineData(true, 320)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task LongLabels_StayBoundedAndActionsRemainAligned(bool dark, int width) => _session.Dispatch(() =>
    {
        var row = new AppSettingsItemRow
        {
            Title = new string('名', 160),
            Subtitle = new string('x', 160),
            Metadata = new string('m', 160),
            BadgeText = new string('b', 160),
            PrimaryLabel = "测试",
            SecondaryLabel = "编辑",
            ActionTarget = new object(),
            PrimaryCommand = new RelayCommand(() => { }),
            SecondaryCommand = new RelayCommand(() => { })
        };
        var window = new AppWindow
        {
            Width = width,
            Height = 160,
            Content = row,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var title = row.FindControl<AppText>("TitleText")!;
            var metadata = row.FindControl<AppText>("MetadataText")!;
            var badge = row.FindControl<AppText>("BadgeTextControl")!;
            var subtitle = row.FindControl<AppText>("SubtitleText")!;
            var primary = row.FindControl<AppButton>("PrimaryActionButton")!;
            var secondary = row.FindControl<AppButton>("SecondaryActionButton")!;
            foreach (var label in new[] { title, metadata, badge, subtitle })
            {
                Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
                Assert.InRange(Right(label, row), 0, Origin(primary, row).X);
                Assert.Equal(label.Text, ToolTip.GetTip(label));
            }
            Assert.True(title.Bounds.Width > 0);
            Assert.InRange(Right(title, row), 0, Origin(metadata, row).X);
            Assert.InRange(Right(metadata, row), 0, Origin(badge, row).X);
            Assert.InRange(Math.Abs(Center(title, row) - Center(metadata, row)), 0, 0.5);
            Assert.InRange(Math.Abs(Center(title, row) - Center(badge, row)), 0, 0.5);
            Assert.InRange(Math.Abs(Center(primary, row) - Center(secondary, row)), 0, 0.5);
            Assert.Equal(24, primary.Bounds.Height);
            Assert.Equal(24, secondary.Bounds.Height);
            Assert.InRange(Right(secondary, row), 0, row.Bounds.Width);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SelectionOnlyRows_HideUnusedSlotsAndKeepSharedVariants(bool dark) => _session.Dispatch(() =>
    {
        var row = new AppSettingsItemRow { Title = "代理", BadgeText = "已启用" };
        row.Classes.Add("compact");
        var window = new AppWindow
        {
            Width = 320,
            Height = 160,
            Content = row,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.False(row.FindControl<AppButton>("PrimaryActionButton")!.IsVisible);
            Assert.False(row.FindControl<AppButton>("SecondaryActionButton")!.IsVisible);
            Assert.False(row.FindControl<AppText>("SubtitleText")!.IsVisible);
            Assert.False(row.FindControl<AppText>("MetadataText")!.IsVisible);
            var surface = Assert.IsType<AppBorder>(row.Content);
            Assert.Equal(new Thickness(8), surface.Padding);
            row.Classes.Remove("compact");
            row.Classes.Add("multiline");
            row.Subtitle = new string('长', 160);
            row.IsBadgeVisible = false;
            window.UpdateLayout();
            var subtitle = row.FindControl<AppText>("SubtitleText")!;
            Assert.True(subtitle.IsVisible);
            Assert.Equal(TextWrapping.Wrap, subtitle.TextWrapping);
            Assert.Equal(2, subtitle.MaxLines);
            Assert.False(row.FindControl<AppText>("BadgeTextControl")!.IsVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static Point Origin(Control control, Control relative) => control.TranslatePoint(new Point(), relative)!.Value;
    private static double Right(Control control, Control relative) => Origin(control, relative).X + control.Bounds.Width;
    private static double Center(Control control, Control relative) => Origin(control, relative).Y + control.Bounds.Height / 2;
}
