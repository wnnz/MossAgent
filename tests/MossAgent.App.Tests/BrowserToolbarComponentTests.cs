using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Analyzers;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserToolbarComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(Avalonia.Application));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task AddressEditingAndEnter_UseTheSameNavigationCommand() => _session.Dispatch(() =>
    {
        var invoked = 0;
        var command = new RelayCommand(() => invoked++);
        var toolbar = new AppBrowserToolbar { Address = "https://example.com/first", NavigateCommand = command };
        var input = toolbar.FindControl<AppInput>("AddressInput")!;
        var navigate = toolbar.FindControl<AppIconButton>("NavigateButton")!;

        Assert.Equal(toolbar.Address, input.Text);
        input.Text = "https://example.com/second";
        Assert.Equal(input.Text, toolbar.Address);
        toolbar.Address = "https://example.com/third";
        Assert.Equal(toolbar.Address, input.Text);
        Assert.Same(command, navigate.Command);
        Assert.Same(command, Assert.Single(input.KeyBindings).Command);
        var enter = new KeyEventArgs { Key = Key.Enter };
        Assert.Single(input.KeyBindings).TryHandle(enter);
        Assert.True(enter.Handled);
        Assert.Equal(1, invoked);
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(input)));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task BusinessDataContext_CannotReplaceComponentOwnedBindings() => _session.Dispatch(() =>
    {
        var command = new RelayCommand(() => { });
        var toolbar = new AppBrowserToolbar { Address = "https://example.com/owned", NavigateCommand = command };
        toolbar.DataContext = new { Address = "https://example.com/unrelated", NavigateCommand = new RelayCommand(() => { }) };

        Assert.Equal(toolbar.Address, toolbar.FindControl<AppInput>("AddressInput")!.Text);
        Assert.Same(command, toolbar.FindControl<AppIconButton>("NavigateButton")!.Command);
        toolbar.DataContext = null;
        toolbar.NavigateCommand = null;
        Assert.Null(toolbar.FindControl<AppIconButton>("NavigateButton")!.Command);
        var enter = new KeyEventArgs { Key = Key.Enter };
        Assert.Single(toolbar.FindControl<AppInput>("AddressInput")!.KeyBindings).TryHandle(enter);
        Assert.False(enter.Handled);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("BackCommand", "BackButton")]
    [InlineData("ForwardCommand", "ForwardButton")]
    [InlineData("ReloadCommand", "ReloadButton")]
    [InlineData("DevToolsCommand", "DevToolsButton")]
    public Task BrowserActions_KeepTheirCommandsAndAvailability(string property, string name) => _session.Dispatch(() =>
    {
        var enabled = false;
        var command = new RelayCommand(() => { }, () => enabled);
        var toolbar = new AppBrowserToolbar();
        toolbar.GetType().GetProperty(property)!.SetValue(toolbar, command);
        var button = toolbar.FindControl<AppIconButton>(name)!;
        var window = new Window { Content = toolbar };
        window.Show();
        try
        {
            Assert.Same(command, button.Command);
            Assert.False(button.Command!.CanExecute(button.CommandParameter));
            Assert.False(button.IsEffectivelyEnabled);
            enabled = true;
            command.NotifyCanExecuteChanged();
            Assert.True(button.Command.CanExecute(button.CommandParameter));
            Assert.True(button.IsEffectivelyEnabled);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(240)]
    [InlineData(277)]
    [InlineData(345)]
    [InlineData(600)]
    public Task CompactRows_ReserveTheAddressWidthWithoutOverlappingActions(double width) => _session.Dispatch(() =>
    {
        var toolbar = new AppBrowserToolbar();
        var layout = Assert.IsType<Grid>(toolbar.Content);
        var input = toolbar.FindControl<AppInput>("AddressInput")!;
        input.Height = 28;
        var buttons = new[] { "NavigateButton", "BackButton", "ForwardButton", "ReloadButton", "DevToolsButton" }
            .Select(name => toolbar.FindControl<AppIconButton>(name)!).ToArray();
        foreach (var button in buttons)
        {
            button.Width = button.MinWidth = 28;
            button.Height = 28;
        }
        layout.Measure(new Size(width, double.PositiveInfinity));
        layout.Arrange(new Rect(0, 0, width, layout.DesiredSize.Height));

        var address = Bounds(input, layout);
        Assert.Equal(width - 32, address.Width);
        foreach (var button in buttons)
        {
            var bounds = Bounds(button, layout);
            Assert.InRange(bounds.Left, 0, width);
            Assert.InRange(bounds.Right, 0, width);
            Assert.False(address.Intersects(bounds));
        }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("var toolbar = new AppBrowserToolbar(); toolbar.Content = new Grid();")]
    [InlineData("var toolbar = new AppBrowserToolbar(); toolbar.SetValue(UserControl.ContentProperty, new Grid());")]
    [InlineData("var toolbar = new AppBrowserToolbar(); toolbar.ClearValue(UserControl.ContentProperty);")]
    public async Task BusinessCode_CannotReplaceOwnedToolbarContent(string body)
    {
        var source = "using Avalonia.Controls; using MossAgent.App.Controls; namespace MossAgent.App.Views; "
            + "public class View { public void Configure() { " + body + " } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public void MarkupGuard_RejectsContentOverridesButAllowsFunctionalBindings()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedComposedControls");
        var query = Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;

        foreach (var markup in new[] { "<AppBrowserToolbar Content='replacement' />",
                     "<AppBrowserToolbar><UserControl.Content><Grid /></UserControl.Content></AppBrowserToolbar>",
                     "<AppBrowserToolbar><Grid /></AppBrowserToolbar>" })
            Assert.Single(XDocument.Parse(markup).XPathSelectElements(query));
        Assert.Empty(XDocument.Parse("<AppBrowserToolbar Address='{Binding Address}' NavigateCommand='{Binding NavigateCommand}' />")
            .XPathSelectElements(query));
    }

    private static Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

}
