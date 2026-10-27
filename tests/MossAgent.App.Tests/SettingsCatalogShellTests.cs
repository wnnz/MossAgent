using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsCatalogShellTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("proxy", false, 526)]
    [InlineData("proxy", true, 526)]
    [InlineData("proxy", false, 1280)]
    [InlineData("proxy", true, 1280)]
    [InlineData("mcp", false, 526)]
    [InlineData("mcp", true, 526)]
    [InlineData("mcp", false, 1280)]
    [InlineData("mcp", true, 1280)]
    public Task CompleteSettingsShellKeepsLastCatalogActionVisibleAndPersistsItsEdit(string page, bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await SettingsCatalogShellFixture.CreateAsync(page, dark, width);
            Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, fixture.Window.ActualThemeVariant);
            var pane = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppUserControl>(),
                control => control.IsEffectivelyVisible && (page == "proxy" ? control is ProxySettingsPane : control is McpSettingsPane));
            var list = Assert.Single(pane.GetVisualDescendants().OfType<AppListBox>());
            Assert.Equal(30, list.ItemCount);
            Assert.True(new Rect(fixture.Window.ClientSize).Contains(new Rect(list.TranslatePoint(default, fixture.Window)!.Value, list.Bounds.Size)));
            Assert.True(Assert.IsAssignableFrom<Control>(list.ContainerFromIndex(0)).Focus(NavigationMethod.Tab));
            fixture.Window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
            fixture.Window.KeyRelease(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
            ProviderUiWorkflowInput.Flush(fixture.Window);
            var row = Assert.Single(list.GetVisualDescendants().OfType<AppSettingsItemRow>(), control => ReferenceEquals(control.ActionTarget, list.SelectedItem));
            Assert.Same(list.Items[29], row.ActionTarget);
            var target = row.ActionTarget;
            var edit = row.FindControl<AppButton>("SecondaryActionButton")!;
            var center = edit.TranslatePoint(new Point(edit.Bounds.Width / 2, edit.Bounds.Height / 2), fixture.Window)!.Value;
            Assert.True(new Rect(fixture.Window.ClientSize).Contains(center));
            var hit = Assert.IsAssignableFrom<Visual>(fixture.Window.InputHitTest(center));
            Assert.True(ReferenceEquals(edit, hit) || edit.IsVisualAncestorOf(hit));
            fixture.Save($"catalog-shell-{page}-{dark}-{width}");
            ProviderUiWorkflowInput.Press(fixture.Window, edit);
            var name = Assert.Single(pane.GetVisualDescendants().OfType<AppInput>(), input => input.IsEffectivelyVisible && input.Text == row.Title);
            ProviderUiWorkflowInput.Type(fixture.Window, name, "Z shell edited item");
            var save = page == "proxy" ? fixture.Main.Proxies.SaveCommand : (IAsyncRelayCommand)fixture.Main.Mcp.SaveCommand;
            ProviderUiWorkflowInput.Press(fixture.Window, Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(), control => ReferenceEquals(control.Command, save)));
            await save.ExecutionTask!.WaitAsync(fixture.Token);
            ProviderUiWorkflowInput.Flush(fixture.Window);
            Assert.False(page == "proxy" ? fixture.Main.Proxies.IsFormOpen : fixture.Main.Mcp.IsFormOpen);
            await AssertSavedAsync(fixture, target);
        }, TestContext.Current.CancellationToken);

    private static async Task AssertSavedAsync(SettingsCatalogShellFixture fixture, object? target)
    {
        if (target is ProxyProfile proxy)
        {
            var saved = await fixture.Configurations.GetProxiesAsync(fixture.Token);
            Assert.Equal(30, saved.Count);
            Assert.Equal(proxy with { Name = "Z shell edited item" }, Assert.Single(saved, item => item.Id == proxy.Id));
        }
        else
        {
            var server = Assert.IsType<McpServerProfile>(target);
            var saved = await fixture.Servers.GetAllAsync(fixture.Token);
            Assert.Equal(30, saved.Count);
            Assert.Equal(server with { Name = "Z shell edited item", Revision = server.Revision + 1 },
                Assert.Single(saved, item => item.Id == server.Id));
        }
    }
}
