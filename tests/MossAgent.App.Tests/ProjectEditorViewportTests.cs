using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectEditorViewportTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601, 1)]
    [InlineData(true, 526, 601, 1)]
    [InlineData(false, 1280, 820, 1)]
    [InlineData(true, 1280, 820, 1)]
    [InlineData(false, 526, 601, 1.25)]
    [InlineData(true, 526, 601, 1.25)]
    [InlineData(false, 526, 601, 2)]
    [InlineData(true, 526, 601, 2)]
    [InlineData(false, 1280, 820, 2)]
    [InlineData(true, 1280, 820, 2)]
    public Task ManyDirectories_KeepEditorAndSaveWithinOwnerViewport(bool dark, int width, int height, double scale) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, width, height, scale);
            fixture.Open();
            fixture.SaveEvidence(FormattableString.Invariant($"project-editor-{dark}-{width}-{scale}x"));
            Assert.True(fixture.Content.Bounds.Height <= fixture.Window.ClientSize.Height - 32,
                $"Project editor {fixture.Content.Bounds} exceeds owner {fixture.Window.ClientSize}.");
            var scroll = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            Assert.Equal(0, scroll.Offset.Y);
            fixture.AssertHit(fixture.SaveButton);
            Assert.All(fixture.Content.GetVisualDescendants().OfType<AppBorder>()
                .Where(border => border.Classes.Contains("project-directory-row")),
                border => Assert.Equal(default, border.BorderThickness));
            var savedOrigin = fixture.SaveButton.TranslatePoint(default, fixture.Popup);
            var last = fixture.Content.GetVisualDescendants().OfType<AppBorder>()
                .Last(border => border.Classes.Contains("project-directory-row"));
            last.BringIntoView();
            fixture.Flush();
            Assert.True(scroll.Offset.Y > 0);
            Assert.Equal(savedOrigin, fixture.SaveButton.TranslatePoint(default, fixture.Popup));
            var remove = Assert.Single(last.GetVisualDescendants().OfType<AppIconButton>());
            fixture.AssertHit(remove);
            fixture.AssertHit(fixture.SaveButton);
            var label = Assert.Single(last.GetVisualDescendants().OfType<AppText>(), text => Equals(text.DataContext, last.DataContext)
                && text.Text == fixture.Editor.Directories[^1].DisplayName);
            var path = Assert.Single(last.GetVisualDescendants().OfType<AppText>(), text => text.Text == fixture.Editor.Directories[^1].Path);
            var textGroup = Assert.IsType<StackPanel>(label.GetVisualParent());
            Assert.True(textGroup.IsVisualAncestorOf(path));
            var icon = last.GetVisualDescendants().OfType<AppIcon>().First();
            var labelCenter = textGroup.TranslatePoint(new Point(0, textGroup.Bounds.Height / 2), last)!.Value.Y;
            var iconCenter = icon.TranslatePoint(new Point(0, icon.Bounds.Height / 2), last)!.Value.Y;
            Assert.True(Math.Abs(labelCenter - iconCenter) <= 0.5 / scale,
                $"Label {label.Bounds} at {labelCenter}; icon {icon.Bounds} at {iconCenter}; row {last.Bounds}; scale {scale}.");
            fixture.SaveEvidence(FormattableString.Invariant($"project-editor-last-{dark}-{width}-{scale}x"));
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PrimaryDirectoryAction_IsVisibleForKeyboardFocus(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProjectEditorViewportFixture.CreateAsync(dark, 526, 601, 1, count: 2);
            fixture.Open();
            var action = Assert.Single(fixture.Content.GetVisualDescendants().OfType<AppButton>(),
                button => button.Classes.Contains("set-primary") && button.IsVisible);
            Assert.True(action.Focus());
            fixture.Flush();
            fixture.SaveEvidence($"project-primary-focus-{dark}");
            Assert.Equal(1, action.Opacity);
            Assert.True(action.IsHitTestVisible);
        }, TestContext.Current.CancellationToken);
}
