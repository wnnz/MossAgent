using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsItemPageFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProxyPage_UsesClickedTargetAndSafeEndpointInsteadOfPreviousSelection(bool dark) => _session.DispatchAsync(async () =>
    {
        var tester = new RecordingProxyConnectionTester();
        var settings = new ProxySettingsViewModel(new ModelReadProbeConfigurationRepository([], []), tester);
        await settings.RefreshCommand.ExecuteAsync(null);
        var first = new ProxyProfile(Guid.NewGuid(), "first", ProxyProtocol.Http, "127.0.0.1", 8080,
            "private-user", "private-password", false, true);
        var second = first with { Id = Guid.NewGuid(), Name = "second", Protocol = ProxyProtocol.Socks5, Port = 1080, IsDefault = true };
        settings.Items.Add(first);
        settings.Items.Add(second);
        settings.SelectedItem = first;
        var pane = new ProxySettingsPane { DataContext = settings };
        var window = new AppWindow
        {
            Width = 980,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var row = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsItemRow>(), item => item.Title == second.Name);
            Assert.Same(second, row.ActionTarget);
            Assert.Equal("127.0.0.1:1080", row.Subtitle);
            Assert.Equal("Socks5", row.Metadata);
            Assert.True(row.HasBadge);
            PressSpace(row.FindControl<AppButton>("PrimaryActionButton")!);
            await settings.TestCommand.ExecutionTask!;
            Assert.Same(second, Assert.Single(tester.Tested));
            Assert.Same(first, settings.SelectedItem);
            PressSpace(row.FindControl<AppButton>("SecondaryActionButton")!);
            Assert.True(settings.IsFormOpen);
            Assert.Same(second, settings.SelectedItem);
            Assert.Equal(second.Password, settings.Password);
            settings.CloseFormCommand.Execute(null);
            settings.Items[1] = second with { IsEnabled = false, IsDefault = false };
            window.UpdateLayout();
            var replacement = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsItemRow>(), item => item.Title == second.Name);
            Assert.False(replacement.FindControl<AppButton>("PrimaryActionButton")!.IsEffectivelyEnabled);
            Assert.False(replacement.HasBadge);
            Assert.DoesNotContain("private-", string.Join(" ", replacement.GetVisualDescendants().OfType<AppText>().Select(text => text.Text)));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task McpPage_BindsToggleAndEditToCurrentRowWithoutShowingCredentials(bool dark) => _session.Dispatch(() =>
    {
        var first = new McpServerProfile(Guid.NewGuid(), "first", McpTransportKind.StreamableHttp,
            "private-command", "[\"private-argument\"]", null, "{\"TOKEN\":\"private-environment\"}",
            new Uri("https://private-user:private-password@fixture.example.test/mcp?token=private-query"),
            "{\"Authorization\":\"private-header\"}", null, false);
        var second = first with { Id = Guid.NewGuid(), Name = "second" };
        var items = new ObservableCollection<McpServerProfile> { first, second };
        McpServerProfile? toggled = null;
        McpServerProfile? edited = null;
        var pane = new McpSettingsPane
        {
            DataContext = new
            {
                Items = items,
                SelectedItem = first,
                HasNoItems = false,
                HasStatus = false,
                ToggleEnabledCommand = new RelayCommand<McpServerProfile>(target => toggled = target),
                OpenEditFormCommand = new RelayCommand<McpServerProfile>(target => edited = target)
            }
        };
        var window = new AppWindow
        {
            Width = 980,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var row = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsItemRow>(), item => item.Title == second.Name);
            Assert.Equal("Streamable HTTP · https://fixture.example.test", row.Subtitle);
            Assert.Equal("启用", row.PrimaryActionLabel);
            PressSpace(row.FindControl<AppButton>("PrimaryActionButton")!);
            PressSpace(row.FindControl<AppButton>("SecondaryActionButton")!);
            Assert.Same(second, toggled);
            Assert.Same(second, edited);
            items[1] = second with { IsEnabled = true };
            window.UpdateLayout();
            var replacement = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsItemRow>(), item => item.Title == second.Name);
            Assert.Equal("停用", replacement.PrimaryActionLabel);
            Assert.DoesNotContain("private-", string.Join(" ", replacement.GetVisualDescendants().OfType<AppText>().Select(text => text.Text)));
            PressSpace(replacement.FindControl<AppButton>("PrimaryActionButton")!);
            Assert.Same(items[1], toggled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
