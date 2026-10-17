using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsCatalogViewportTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("proxy", false, 486)]
    [InlineData("proxy", true, 486)]
    [InlineData("proxy", false, 1024)]
    [InlineData("proxy", true, 1024)]
    [InlineData("mcp", false, 486)]
    [InlineData("mcp", true, 486)]
    [InlineData("mcp", false, 1024)]
    [InlineData("mcp", true, 1024)]
    public Task LongCatalogKeepsItsViewportAndLastRowActionsInsideThePage(string page, bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            using var fixture = new SettingsCatalogViewportFixture(page, dark, width);
            await fixture.Refresh.ExecuteAsync(null);
            fixture.Flush();
            fixture.Save($"catalog-{page}-{dark}-{width}");
            fixture.AssertContained(fixture.List);
            var row = fixture.SelectLastRow();
            var edit = row.FindControl<AppButton>("SecondaryActionButton")!;
            fixture.AssertContained(edit);
            fixture.AssertHit(edit);
            fixture.Save($"catalog-last-{page}-{dark}-{width}");
            fixture.Press(edit);
            Assert.True(fixture.IsFormOpen());
            fixture.Send(Key.Escape);
            Assert.False(fixture.IsFormOpen());
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("proxy", false, 486)]
    [InlineData("proxy", true, 486)]
    [InlineData("proxy", false, 1024)]
    [InlineData("proxy", true, 1024)]
    [InlineData("mcp", false, 486)]
    [InlineData("mcp", true, 486)]
    [InlineData("mcp", false, 1024)]
    [InlineData("mcp", true, 1024)]
    public Task EmptyAndSingleItemCatalogsKeepNaturalHeightAndCreateActions(string page, bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            foreach (var count in new[] { 0, 1 })
            {
                using var fixture = new SettingsCatalogViewportFixture(page, dark, width, count);
                await fixture.Refresh.ExecuteAsync(null);
                fixture.Flush();
                Assert.Equal(count, fixture.List.ItemCount);
                fixture.AssertContained(fixture.Card);
                Assert.InRange(fixture.Card.Bounds.Height, 1, 160);
                var create = Assert.Single(fixture.Header.GetVisualDescendants().OfType<AppActionButton>(),
                    button => button.Classes.Contains("primary"));
                fixture.AssertHit(create);
                fixture.Press(create);
                Assert.True(fixture.IsFormOpen());
                fixture.Send(Key.Escape);
                Assert.False(fixture.IsFormOpen());
            }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("proxy", false)]
    [InlineData("proxy", true)]
    [InlineData("mcp", false)]
    [InlineData("mcp", true)]
    public Task RepeatedResizingPreservesSelectionAndReachableActions(string page, bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SettingsCatalogViewportFixture(page, dark, 1024);
        await fixture.Refresh.ExecuteAsync(null);
        fixture.Flush();
        var row = fixture.SelectLastRow();
        var selected = fixture.List.SelectedItem;
        foreach (var size in new[] { (1024, 820), (486, 601), (1024, 601), (486, 820), (1024, 820) })
        {
            fixture.Window.Width = size.Item1;
            fixture.Window.Height = size.Item2;
            fixture.Flush();
            fixture.AssertContained(fixture.Card);
            fixture.AssertContained(fixture.List);
            Assert.Same(selected, fixture.List.SelectedItem);
            var edit = row.FindControl<AppButton>("SecondaryActionButton")!;
            edit.BringIntoView();
            fixture.Flush();
            fixture.AssertContained(edit);
            fixture.AssertHit(edit);
        }
    }, TestContext.Current.CancellationToken);
}
