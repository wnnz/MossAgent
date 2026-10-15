using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmptySessionHeadingLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(360, false)]
    [InlineData(360, true)]
    [InlineData(640, false)]
    [InlineData(640, true)]
    public Task EmptyConversation_KeepsProjectHeadingInsideViewport(double width, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var view = new WorkspaceChatView { DataContext = fixture.Workspace };
        var window = new AppWindow
        {
            DataContext = fixture.Main,
            Width = width,
            Height = 590,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var fragments = view.GetVisualDescendants().OfType<AppText>().Where(text =>
                text.Text == fixture.Workspace.EmptySessionPrefix || text.Text == fixture.Workspace.EmptySessionSuffix).ToArray();
            Assert.Equal(2, fragments.Length);
            foreach (var text in fragments)
            {
                var position = text.TranslatePoint(default, view)!.Value;
                Assert.True(position.X >= 0 && position.X + text.Bounds.Width <= view.Bounds.Width,
                    $"Heading '{text.Text}' at {position} {text.Bounds.Size} exceeds viewport {view.Bounds.Size}");
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
