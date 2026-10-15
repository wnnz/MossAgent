using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using SkiaSharp;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileCardFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(IconDrawingTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280)]
    [InlineData(false, 740)]
    [InlineData(true, 280)]
    [InlineData(true, 740)]
    public async Task TypographyAndMoreAction_PreserveAlignedBoundedRowsAndRealCommands(bool dark, int width)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await _session.DispatchAsync(async () =>
        {
            await fixture.Workspace.LoadAsync();
            fixture.Workspace.FileChanges.Dispose();
            using var summary = new WorkspaceFileChangesViewModel(fixture.Workspace.Catalog, fixture.Panels,
                statisticsService: new WorkspaceFileChangeStatisticsService(new FileStatisticsTestExecutor()));
            for (var index = 0; index < 4; index++)
                fixture.Workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("filesystem.write_file", true, "written", null,
                    FileChanges: [new(Path.Combine(fixture.Project.PrimaryDirectory, $"a-long-directory-name-{index}/source-file.cs"), new string('A', 64), new string('B', 64))])));
            var view = new AppFileChangeSummary { Summary = summary };
            var window = new AppWindow { Width = width, Height = 420, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                await FileChangeStatisticsStateTests.WaitAsync(() => summary.Statistics.HasTotals);
                window.UpdateLayout();
                var rows = view.GetVisualDescendants().OfType<AppButton>().Where(button => button.Classes.Contains("file-change-file")).ToArray();
                Assert.Equal(3, rows.Length);
                foreach (var row in rows) AssertRow(row, view, summary);
                var actions = view.GetVisualDescendants().OfType<AppButton>().Where(button => ReferenceEquals(button.Command, summary.Undo.RequestCommand) || ReferenceEquals(button.Command, summary.ReviewCommand)).ToArray();
                Assert.Equal(2, actions.Length);
                foreach (var action in actions)
                {
                    Assert.Equal(13, action.FontSize);
                    Assert.Equal(28, action.Bounds.Height);
                }
                var more = Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(), button => button.Classes.Contains("file-change-more"));
                Assert.Equal(13, more.FontSize);
                Assert.Equal(34, more.Bounds.Height);
                Assert.Same(summary.ExpandCommand, more.Command);
                Assert.Equal(view.FindResource(window.ActualThemeVariant, "TextPrimaryBrush"), more.Foreground);
                var chevron = Assert.Single(more.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsEffectivelyVisible);
                Assert.Equal(8, chevron.Size);
                Assert.Equal(new Thickness(10, 0, 0, 0), chevron.Margin);
                var label = Assert.Single(more.GetVisualDescendants().OfType<AppText>(), text => text.IsEffectivelyVisible);
                Assert.Equal(13, label.FontSize);
                Assert.InRange(Math.Abs(CenterY(chevron, view) - CenterY(label, view)), 0, 0.5);
                more.Command!.Execute(null);
                window.UpdateLayout();
                Assert.Equal(4, summary.VisibleFiles.Count);
                Assert.False(more.IsVisible);
                foreach (var row in view.GetVisualDescendants().OfType<AppButton>().Where(button => button.Classes.Contains("file-change-file")))
                    AssertRow(row, view, summary);
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                window.UpdateLayout();
                foreach (var row in view.GetVisualDescendants().OfType<AppButton>().Where(button => button.Classes.Contains("file-change-file")))
                    AssertRow(row, view, summary);
                await summary.StopAsync();
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 1.25)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 1.25)]
    [InlineData(true, 2)]
    public Task SummaryGlyph_HasCompactSquareContourSeparatePlusAndMinusAtRenderScale(bool dark, double scale) => _session.DispatchAsync(() =>
    {
        var surface = new AppBorder { Classes = { "file-change-icon-surface" }, Child = new AppIcon { Classes = { "ui-icon", "file-change-summary-icon" } } };
        var window = new AppWindow { Width = 40, Height = 40, Content = surface, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var icon = Assert.IsType<AppIcon>(surface.Child);
            Assert.Equal(18, icon.Size);
            Assert.Equal(new Rect(2, 2, 20, 20), icon.Data!.Bounds);
            Assert.Equal(40, surface.Bounds.Width);
            Assert.Equal(40, surface.Bounds.Height);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)(40 * scale), (int)(40 * scale)), new Vector(96 * scale, 96 * scale));
            bitmap.Render(surface);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            using var pixels = SKBitmap.Decode(stream.ToArray());
            var background = pixels.GetPixel((int)(20 * scale), (int)(23 * scale));
            Assert.NotEqual(background, pixels.GetPixel((int)(20 * scale), (int)(18 * scale)));
            Assert.NotEqual(background, pixels.GetPixel((int)(20 * scale), (int)(25 * scale)));
            Assert.Equal(background, pixels.GetPixel((int)(20 * scale), (int)(9 * scale)));
            Assert.Equal(background, pixels.GetPixel((int)(9 * scale), (int)(20 * scale)));
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TrailingSpacing_UsesScopedStylesAndRestoresDefaultsWithoutAffectingOtherActions() => _session.DispatchAsync(() =>
    {
        var normal = new AppActionButton { Label = "普通", TrailingIconData = Geometry.Parse("M 6 9 L 12 15 L 18 9") };
        var more = new AppActionButton { Classes = { "file-change-more" }, Label = "展开", TrailingIconData = normal.TrailingIconData };
        Grid.SetRow(more, 1);
        var window = new AppWindow { Width = 300, Height = 120, Content = new Grid { RowDefinitions = new("Auto,Auto"), Children = { normal, more } } };
        window.Show();
        try
        {
            window.UpdateLayout();
            var normalIcon = Assert.Single(normal.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsEffectivelyVisible);
            var moreIcon = Assert.Single(more.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsEffectivelyVisible);
            Assert.Equal(3, normal.TrailingIconSpacing);
            Assert.Equal(3, normalIcon.Margin.Left);
            Assert.Equal(10, more.TrailingIconSpacing);
            Assert.Equal(10, moreIcon.Margin.Left);
            more.TrailingIconSpacing = 6;
            Assert.Equal(6, moreIcon.Margin.Left);
            more.ClearValue(AppActionButton.TrailingIconSpacingProperty);
            Assert.Equal(10, moreIcon.Margin.Left);
            Assert.Equal(3, normalIcon.Margin.Left);
            Assert.Throws<ArgumentException>(() => more.TrailingIconSpacing = -1);
            Assert.Throws<ArgumentException>(() => more.TrailingIconSpacing = double.NaN);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    private static void AssertRow(AppButton row, AppFileChangeSummary view, WorkspaceFileChangesViewModel summary)
    {
        Assert.Equal(36, row.Bounds.Height);
        Assert.Same(summary.OpenFileCommand, row.Command);
        var path = Assert.Single(row.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("file-change-path"));
        Assert.Equal(13, path.FontSize);
        var numbers = Assert.Single(row.GetVisualDescendants().OfType<AppDiffStatistics>());
        var values = numbers.GetVisualDescendants().OfType<AppText>().ToArray();
        Assert.Equal(view.FindResource(view.ActualThemeVariant, "FileChangeStatisticsAddedBrush"), values[0].Foreground);
        Assert.Equal(view.FindResource(view.ActualThemeVariant, "FileChangeStatisticsRemovedBrush"), values[1].Foreground);
        Assert.InRange(Math.Abs(CenterY(path, view) - CenterY(numbers, view)), 0, 0.5);
        var pathOrigin = path.TranslatePoint(default, view)!.Value;
        var numbersOrigin = numbers.TranslatePoint(default, view)!.Value;
        Assert.True(pathOrigin.X + path.Bounds.Width <= numbersOrigin.X);
        Assert.InRange(numbersOrigin.X + numbers.Bounds.Width, 0, view.Bounds.Width);
    }

    private static double CenterY(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo)!.Value.Y + visual.Bounds.Height / 2;
}
