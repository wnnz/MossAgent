using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Analyzers;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingCommands_DisableEveryOwnedAction(bool dark) => _session.Dispatch(() =>
    {
        var header = new AppInspectorHeader { ActiveTab = "diff" };
        var window = new AppWindow
        {
            Width = 320,
            Height = 140,
            Content = header,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.All(header.GetVisualDescendants().OfType<AppIconButton>(), button => Assert.False(button.IsEffectivelyEnabled));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(980, false)]
    [InlineData(1280, false)]
    [InlineData(1920, false)]
    [InlineData(980, true)]
    [InlineData(1280, true)]
    [InlineData(1920, true)]
    public Task PersistentTabs_AreAlignedNavigateAndRespectPaneBounds(int width, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var created = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, "合成终端界面任务",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, created, created, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = WindowState.Normal,
            Width = width,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            fixture.Main.ToggleRightPanelCommand.Execute(null);
            window.UpdateLayout();
            var header = Assert.Single(window.GetVisualDescendants().OfType<AppInspectorHeader>());
            var buttons = new[] { "SourcesButton", "DiffButton", "TerminalButton", "BrowserButton", "AuditButton" }
                .Select(name => header.FindControl<AppIconButton>(name)!).ToArray();
            var centers = buttons.Select(button => button.TranslatePoint(new Point(16, 16), window)!.Value.Y).ToArray();
            Assert.All(centers, center => Assert.Equal(centers[0], center));
            Assert.All(buttons, button =>
            {
                Assert.Equal(new Size(32, 32), button.Bounds.Size);
                Assert.Equal(15, Assert.Single(button.GetVisualDescendants().OfType<AppIcon>()).Size);
                Assert.True(button.IsEffectivelyVisible);
            });
            var draft = fixture.Workspace.ComposerText;
            foreach (var button in buttons)
            {
                Assert.True(button.Focus(NavigationMethod.Tab));
                button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
                Assert.Equal(button.CommandParameter, fixture.Main.ActiveInspectorTab);
                Assert.Contains("selected", button.Classes);
                Assert.Single(buttons, item => item.Classes.Contains("selected"));
                if (Equals(button.CommandParameter, "terminal"))
                {
                    window.UpdateLayout();
                    var terminal = Assert.Single(window.GetVisualDescendants().OfType<AppTerminalPane>());
                    Assert.True(terminal.IsEffectivelyVisible);
                    Assert.Same(fixture.Main.Terminal, terminal.DataContext);
                }
            }
            Assert.Equal(draft, fixture.Workspace.ComposerText);
            Assert.Empty(fixture.Server.Requests);
            Assert.False(fixture.Main.SelectInspectorTabCommand.CanExecute("invalid"));
            fixture.Main.ActiveInspectorTab = "invalid";
            Assert.Equal("audit", fixture.Main.ActiveInspectorTab);
            var close = header.FindControl<AppIconButton>("CloseButton")!;
            Assert.Same(fixture.Main.ToggleRightPanelCommand, close.Command);
            Assert.True(close.TranslatePoint(new Point(close.Bounds.Width, 0), window)!.Value.X <= width);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task CodeGuard_RejectsNewComponentsContentAndGeometryReplacement()
    {
        var diagnostics = await VisualPolicyTestCompilation.Analyze("""
            using MossAgent.App.Controls;
            public class Sample
            {
                public void Change(AppInspectorHeader header, AppEmptyState empty)
                {
                    header.Content = new AppText(); header.Width = 500;
                    empty.Content = new AppText(); empty.Padding = new Avalonia.Thickness(2);
                }
            }
            """, new SharedControlAppearanceAnalyzer());
        Assert.Equal(4, diagnostics.Count(diagnostic => diagnostic.Id == "MOSSUI002"));
    }

    [Fact]
    public void MarkupGuard_RejectsCompositionButAllowsCommandsAndSemanticContent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var project = AppBuildDefinition.Load();
        var query = Assert.Single(project.Descendants(), element => element.Attribute("Name")?.Value == "ValidateSharedInspectorComponents")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        var document = XDocument.Parse("""
            <Root xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppInspectorHeader Content="replacement" /><ui:AppEmptyState Width="60" />
              <ui:AppInspectorHeader SelectCommand="binding" Title="Files" />
              <ui:AppEmptyState Title="Empty" Description="No data" />
            </Root>
            """);
        Assert.Equal(2, document.XPathSelectElements(query).Count());
    }
}
