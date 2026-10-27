using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsCatalogMutationTests : IAsyncLifetime
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
    public Task LastRowCanBeEditedSavedAndActedOnWithVisibleFeedback(string page, bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            using var fixture = new SettingsCatalogViewportFixture(page, dark, width);
            await fixture.Refresh.ExecuteAsync(null);
            fixture.Flush();
            var row = fixture.SelectLastRow();
            var target = row.ActionTarget;
            fixture.Press(row.FindControl<AppButton>("SecondaryActionButton")!);
            var name = Assert.Single(fixture.View.GetVisualDescendants().OfType<AppInput>(),
                input => input.IsEffectivelyVisible && input.Text == row.Title);
            fixture.Type(name, "Z updated last item");
            var save = (IAsyncRelayCommand?)fixture.Proxies?.SaveCommand ?? fixture.Mcp!.Model.SaveCommand;
            var button = Assert.Single(fixture.View.GetVisualDescendants().OfType<AppButton>(),
                control => ReferenceEquals(control.Command, save));
            fixture.AssertContained(button);
            fixture.AssertHit(button);
            fixture.Press(button);
            await save.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Flush();
            Assert.False(fixture.IsFormOpen());
            var changed = fixture.SelectLastRow();
            Assert.Equal("Z updated last item", changed.Title);
            AssertSavedTarget(fixture, target);
            fixture.AssertContained(fixture.List);
            var action = changed.FindControl<AppButton>("PrimaryActionButton")!;
            fixture.AssertContained(action);
            fixture.AssertHit(action);
            fixture.Press(action);
            var operation = (IAsyncRelayCommand?)fixture.Proxies?.TestCommand ?? fixture.Mcp!.Model.ToggleEnabledCommand;
            await operation.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Flush();
            Assert.True(fixture.Header.HasStatus);
            Assert.Equal(fixture.Proxies?.Status ?? fixture.Mcp!.Model.Status, fixture.Header.Status);
            fixture.AssertContained(fixture.Header);
            fixture.AssertContained(fixture.List);
            AssertPrimaryTarget(fixture, target);
            fixture.Save($"catalog-feedback-{page}-{dark}-{width}");
        }, TestContext.Current.CancellationToken);

    private static void AssertSavedTarget(SettingsCatalogViewportFixture fixture, object? target)
    {
        if (target is ProxyProfile proxy)
        {
            var saved = Assert.Single(fixture.ProxyRepository!.Proxies, item => item.Id == proxy.Id);
            Assert.Equal(proxy with { Name = "Z updated last item" }, saved);
            Assert.Equal(30, fixture.ProxyRepository.Proxies.Count);
        }
        else
        {
            var server = Assert.IsType<McpServerProfile>(target);
            var saved = Assert.Single(fixture.Mcp!.Repository.Items, item => item.Id == server.Id);
            Assert.Equal(server with { Name = "Z updated last item", Revision = server.Revision + 1 }, saved);
            Assert.Equal(30, fixture.Mcp.Repository.Items.Count);
        }
    }

    private static void AssertPrimaryTarget(SettingsCatalogViewportFixture fixture, object? target)
    {
        if (target is ProxyProfile proxy)
            Assert.Equal(proxy.Id, Assert.Single(fixture.Tester.Tested).Id);
        else
        {
            var server = Assert.IsType<McpServerProfile>(target);
            Assert.True(Assert.Single(fixture.Mcp!.Repository.Items, item => item.Id == server.Id).IsEnabled);
            Assert.All(fixture.Mcp.Repository.Items.Where(item => item.Id != server.Id), item => Assert.False(item.IsEnabled));
        }
    }
}
