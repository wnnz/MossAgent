using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AttachmentChipComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task OwnedBindingsFollowSemanticPropertiesNotTheBusinessDataContext() => _session.Dispatch(() =>
    {
        var chip = new AppAttachmentChip
        {
            DisplayPath = "src/file.txt",
            SizeLabel = "28 B",
            RemoveCommand = new RelayCommand(() => { }),
            DataContext = new { DisplayPath = "incorrect.txt", SizeLabel = "incorrect", RemoveCommand = new RelayCommand(() => { }) }
        };
        Assert.Equal(chip.DisplayPath, chip.FindControl<AppText>("PathLabel")!.Text);
        Assert.Equal(chip.SizeLabel, chip.FindControl<AppText>("SizeText")!.Text);
        Assert.Equal(chip.DisplayPath, ToolTip.GetTip(chip));
        Assert.Contains(chip.DisplayPath, AutomationProperties.GetName(chip));
        Assert.Contains(chip.SizeLabel, AutomationProperties.GetName(chip));
        Assert.Same(chip.RemoveCommand, chip.FindControl<AppIconButton>("RemoveButton")!.Command);
        chip.DisplayPath = "changed.txt";
        chip.SizeLabel = "30 B";
        Assert.Equal(chip.DisplayPath, chip.FindControl<AppText>("PathLabel")!.Text);
        Assert.Equal(chip.SizeLabel, chip.FindControl<AppText>("SizeText")!.Text);
        Assert.Contains("changed.txt", AutomationProperties.GetName(chip));
        chip.SizeLabel = string.Empty;
        Assert.Equal(chip.DisplayPath, AutomationProperties.GetName(chip));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RemovalCommandControlsAvailabilityAndCanBeRemovedOrReplaced() => _session.Dispatch(() =>
    {
        var invoked = 0;
        var enabled = false;
        var command = new RelayCommand(() => invoked++, () => enabled);
        var chip = new AppAttachmentChip { DisplayPath = "file.txt", SizeLabel = "28 B", RemoveCommand = command };
        var button = chip.FindControl<AppIconButton>("RemoveButton")!;
        var window = new AppWindow { Content = chip };
        window.Show();
        try
        {
            Assert.True(chip.HasRemoveAction);
            Assert.True(button.IsVisible);
            Assert.False(button.IsEffectivelyEnabled);
            PressSpace(button);
            Assert.Equal(0, invoked);
            enabled = true;
            command.NotifyCanExecuteChanged();
            Assert.True(button.IsEffectivelyEnabled);
            Assert.True(button.Focus());
            PressSpace(button);
            Assert.Equal(1, invoked);
            chip.RemoveCommand = null;
            Assert.False(chip.HasRemoveAction);
            Assert.False(button.IsVisible);
            Assert.Null(button.Command);
            chip.RemoveCommand = new RelayCommand(() => invoked += 10);
            Assert.True(button.IsVisible);
            Assert.True(button.Focus());
            PressSpace(button);
            Assert.Equal(11, invoked);
            Assert.False(string.IsNullOrWhiteSpace(button.Label));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ComposerAndMessageShareSurfaceTypographyAndCenteredLayout(bool dark, bool removable) => _session.Dispatch(() =>
    {
        foreach (var width in new[] { 150d, 260d, 420d })
        {
            var chip = new AppAttachmentChip
            {
                DisplayPath = "项目目录/很长的附件名称用于验证省略和移除按钮不会重叠.txt",
                SizeLabel = "28 KiB",
                RemoveCommand = removable ? new RelayCommand(() => { }) : null
            };
            var window = new AppWindow
            {
                Width = width,
                Height = 140,
                Content = chip,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var surface = Assert.Single(chip.GetVisualDescendants().OfType<AppBorder>());
                var icon = chip.FindControl<AppIcon>("FileIcon")!;
                var details = chip.FindControl<StackPanel>("FileDetails")!;
                var path = chip.FindControl<AppText>("PathLabel")!;
                var size = chip.FindControl<AppText>("SizeText")!;
                var remove = chip.FindControl<AppIconButton>("RemoveButton")!;
                Assert.Equal(11.5, path.FontSize);
                Assert.Equal(9.5, size.FontSize);
                Assert.Equal(14, icon.Size);
                Assert.Equal(12, surface.CornerRadius.TopLeft);
                Assert.Equal(42, surface.Bounds.Height);
                Assert.Equal(24, remove.Width);
                Assert.Equal(24, remove.Height);
                Assert.Equal(10, Assert.IsType<AppIcon>(remove.Content).Size);
                Assert.NotNull(surface.Background);
                Assert.Equal(1, surface.BoxShadow.Count);
                Assert.Equal(removable, remove.IsVisible);
                Assert.InRange(chip.Bounds.Width, 0, Math.Min(300, width));
                var iconBounds = Bounds(icon, chip);
                var labelBounds = Bounds(details, chip);
                Assert.InRange(Math.Abs(iconBounds.Center.Y - labelBounds.Center.Y), 0, 0.5);
                Assert.True(iconBounds.Right <= labelBounds.Left);
                Assert.True(labelBounds.Right <= chip.Bounds.Width);
                Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, path.TextTrimming);
                if (!removable) continue;
                var removeBounds = Bounds(remove, chip);
                Assert.Equal(new Size(24, 24), remove.Bounds.Size);
                Assert.True(Math.Abs(iconBounds.Center.Y - removeBounds.Center.Y) <= 0.5,
                    $"Icon {iconBounds}; remove {removeBounds}; details {labelBounds}");
                Assert.True(labelBounds.Right <= removeBounds.Left);
                Assert.True(removeBounds.Right <= chip.Bounds.Width);
            }
            finally { window.Close(); }
        }
    }, TestContext.Current.CancellationToken);

    private static Avalonia.Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    private static void PressSpace(AppButton button)
    {
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
