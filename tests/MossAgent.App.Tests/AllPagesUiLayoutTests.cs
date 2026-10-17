using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AllPagesUiLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 980, 700)]
    [InlineData(true, 980, 700)]
    [InlineData(false, 1440, 900)]
    [InlineData(true, 1440, 900)]
    public Task AllPages_RetainNavigationDraftAndContainedHeaders(bool dark, int width, int height) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            await fixture.Main.Browser.Initialization;
            await fixture.Main.Proxies.RefreshCommand.ExecuteAsync(null);
            await fixture.Main.Mcp.ReloadCommand.ExecuteAsync(null);
            await fixture.Main.Agents.RefreshCommand.ExecuteAsync(null);
            await fixture.Main.Subagents.RefreshCommand.ExecuteAsync(null);
            var draft = fixture.Workspace.ComposerText;
            var window = new MainWindow
            {
                DataContext = fixture.Main,
                Width = width,
                Height = height,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                foreach (var page in new[] { "5", "0", "1", "2", "3", "6", "4" })
                {
                    fixture.Main.SelectSettingsTabCommand.Execute(page);
                    Flush(window);
                    Assert.True(fixture.Main.IsSettingsOpen);
                    foreach (var header in window.GetVisualDescendants().OfType<AppSettingsPageHeader>().Where(control => control.IsEffectivelyVisible))
                        AssertContained(header, window);
                    Save(window, $"settings-{page}-{dark}-{width}");
                    if (page == "5") ReviewAdvancedAppearance(window, $"appearance-advanced-{dark}-{width}");
                    if (page is "0" or "1" or "3" or "6" or "4")
                        ReviewEditor(window, $"editor-{page}-{dark}-{width}");
                }
                fixture.Main.CloseSettingsCommand.Execute(null);
                Flush(window);
                Save(window, $"conversation-{dark}-{width}");
                Assert.Equal(draft, fixture.Workspace.ComposerText);
                fixture.Main.OpenAutomationsCommand.Execute(null);
                Flush(window);
                Assert.True(fixture.Main.Pages.IsAutomationsOpen);
                var automationHeader = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsPageHeader>(),
                    control => control.IsEffectivelyVisible);
                Assert.Equal(20, Assert.Single(automationHeader.GetVisualDescendants().OfType<AppText>(),
                    control => control.Classes.Contains("settings-title")).FontSize);
                Save(window, $"automations-{dark}-{width}");
                fixture.Workspace.Catalog.NewTaskCommand.Execute(null);
                var project = fixture.Workspace.Catalog.SelectedProject!;
                var task = new AgentTask(Guid.NewGuid(), project.Id, "界面回归任务", ApprovalPolicy.ReadOnly,
                    AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
                await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
                await fixture.Workspace.Catalog.ReloadAsync();
                fixture.Workspace.Catalog.SelectTaskCommand.Execute(task);
                foreach (var tab in new[] { "home", "diff", "terminal", "browser", "audit" })
                {
                    fixture.Main.SelectInspectorTabCommand.Execute(tab);
                    Flush(window);
                    Assert.Equal(tab, fixture.Main.ActiveInspectorTab);
                    Save(window, $"inspector-{tab}-{dark}-{width}");
                }
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BrowserSettings_KeepDescriptionsReadableInTheCompactShell(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await fixture.Main.Browser.Initialization;
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = 526,
            Height = 601,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            fixture.Main.SelectSettingsTabCommand.Execute("2");
            foreach (var preference in Enum.GetValues<BrowserProfilePreference>())
            {
                fixture.Main.Browser.ProfilePreference = preference;
                Flush(window);
                var page = Assert.Single(window.GetVisualDescendants().OfType<BrowserSettingsPane>());
                foreach (var text in page.GetVisualDescendants().OfType<AppText>()
                    .Where(control => control.IsEffectivelyVisible && control.TextTrimming == Avalonia.Media.TextTrimming.None
                        && !string.IsNullOrWhiteSpace(control.Text)))
                {
                    text.BringIntoView();
                    Flush(window);
                    Assert.True(text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + 1,
                        $"{preference}: {text.Text} needs {text.TextLayout.WidthIncludingTrailingWhitespace}, has {text.Bounds.Width}");
                }
                Save(window, $"browser-{preference}-{dark}-526");
            }
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void ReviewEditor(Window window, string name)
    {
        var create = Assert.Single(window.GetVisualDescendants().OfType<AppActionButton>(),
            control => control.IsEffectivelyVisible && control.Classes.Contains("primary"));
        create.BringIntoView();
        Flush(window);
        Assert.True(create.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.None, null);
        Flush(window);
        var dialog = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialog>(), control => control.IsOpen);
        Save(window, name);
        dialog.CloseCommand!.Execute(null);
        Flush(window);
        Assert.False(dialog.IsOpen);
    }

    private static void ReviewAdvancedAppearance(Window window, string name)
    {
        var advanced = Assert.Single(window.GetVisualDescendants().OfType<AppExpander>(),
            control => control.Classes.Contains("appearance-advanced"));
        advanced.IsExpanded = true;
        Flush(window);
        var rows = window.GetVisualDescendants().OfType<AppSettingsRow>()
            .Where(control => control.IsEffectivelyVisible && control.Classes.Contains("font-control")).ToArray();
        Assert.Equal(2, rows.Length);
        foreach (var row in rows)
        {
            row.BringIntoView();
            Flush(window);
            AssertContained(row, window);
            var label = Assert.IsType<AppText>(row.Children[0]);
            var selector = Assert.IsType<AppComboBox>(row.Children[1]);
            Assert.False(label.Bounds.Intersects(selector.Bounds));
        }
        Save(window, name);
        advanced.IsExpanded = false;
        Flush(window);
    }

    private static void AssertContained(Control control, Window window)
    {
        var bounds = new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
        Assert.True(new Rect(window.ClientSize).Contains(bounds), $"{control.GetType().Name} exceeds the page: {bounds}");
    }

    private static void Save(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
