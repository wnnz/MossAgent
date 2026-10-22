using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Analyzers;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TaskNavigationRowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, "Light")]
    [InlineData(1280, "Light")]
    [InlineData(1920, "Light")]
    [InlineData(980, "Dark")]
    [InlineData(1280, "Dark")]
    [InlineData(1920, "Dark")]
    public Task AllSidebarSections_UseAlignedBoundedRows(double width, string theme) => WithWindow((window, model) =>
    {
        window.Width = width;
        window.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        window.UpdateLayout();
        var rows = window.GetVisualDescendants().OfType<AppTaskNavigationRow>()
            .Where(row => row.IsEffectivelyVisible).ToArray();
        Assert.Equal(6, rows.Length);
        Assert.Equal(2, rows.Count(row => row.IsProjectTask));
        Assert.Equal(4, rows.Count(row => !row.IsProjectTask));
        Assert.Equal(2, rows.Count(row => row.IsSelected));
        Assert.Single(rows, row => Assert.IsType<AgentTask>(row.CommandParameter).IsPinned);
        foreach (var recent in model.Workspace.Catalog.RecentTasks.Items)
            Assert.Single(rows, row => !row.IsProjectTask
                && Assert.IsType<AgentTask>(row.CommandParameter).Id == recent.Task.Id);
        foreach (var row in rows)
        {
            var select = row.FindControl<AppButton>("SelectTaskButton")!;
            var options = row.FindControl<AppIconButton>("TaskOptionsButton")!;
            window.MouseMove(select.TranslatePoint(new Point(60, select.Bounds.Height / 2), window)!.Value);
            window.UpdateLayout();
            var title = Assert.Single(select.GetVisualDescendants().OfType<AppText>());
            Assert.Same(model.Workspace.Catalog.SelectTaskCommand, select.Command);
            Assert.Same(row.CommandParameter, select.CommandParameter);
            Assert.Equal(row.Title, AutomationProperties.GetName(select));
            Assert.Equal(new Size(28, 28), options.Bounds.Size);
            Assert.Equal(30, row.Bounds.Height);
            Assert.Equal(14, title.FontSize);
            var titleBounds = Bounds(title, row);
            var optionsBounds = Bounds(options, row);
            Assert.Equal(row.Bounds.Height / 2 - 1, titleBounds.Center.Y, precision: 3);
            Assert.Equal(row.Bounds.Height / 2, optionsBounds.Center.Y, precision: 3);
            Assert.True(titleBounds.Right <= optionsBounds.Left);
            Assert.True(select.Bounds.Width <= row.Bounds.Width);
            Assert.True(optionsBounds.Right <= row.Bounds.Width);
        }
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData("rename")]
    [InlineData("archive")]
    [InlineData("pin")]
    public Task KeyboardMenu_ActsOnItsRowWithoutOpeningIt(string action) => WithWindow(async (window, model) =>
    {
        var catalog = model.Workspace.Catalog;
        var selectedId = catalog.SelectedTask!.Id;
        var row = Assert.Single(window.GetVisualDescendants().OfType<AppTaskNavigationRow>(),
            item => item.IsEffectivelyVisible && item.IsProjectTask && !item.IsSelected);
        var task = Assert.IsType<AgentTask>(row.CommandParameter);
        Activate(window, row.FindControl<AppIconButton>("TaskOptionsButton")!);
        var menuButton = row.FindControl<AppButton>(action switch
        {
            "rename" => "RenameTaskButton",
            "archive" => "ArchiveTaskButton",
            _ => "PinTaskButton"
        })!;
        Assert.True(menuButton.IsEffectivelyVisible);
        Assert.Same(task, menuButton.CommandParameter);
        Activate(window, menuButton);
        if (action == "rename")
        {
            Assert.True(catalog.TaskManagement.IsEditing);
            Assert.Equal(task.Title, catalog.TaskManagement.EditingTitle);
        }
        else if (action == "archive")
        {
            await catalog.TaskManagement.ArchiveCommand.ExecutionTask!;
            Assert.DoesNotContain(catalog.ProjectNavigation.SelectMany(group => group.Tasks),
                item => item.Task.Id == task.Id);
        }
        else
        {
            await catalog.TaskManagement.TogglePinCommand.ExecutionTask!;
            var pinned = Assert.Single(catalog.TaskManagement.PinnedTasks.Items,
                item => item.Task.Id == task.Id);
            Assert.Equal("取消置顶", pinned.PinToggleLabel);
        }
        Assert.Equal(selectedId, catalog.SelectedTask?.Id);
        window.UpdateLayout();
    });

    [Theory]
    [InlineData("Width = 30")]
    [InlineData("Padding = new Avalonia.Thickness(2)")]
    [InlineData("Content = new AppText()")]
    public async Task BusinessCode_CannotReplaceRowAppearance(string assignment)
    {
        var source = $$"""
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class Page { public void Render() { _ = new AppTaskNavigationRow { {{assignment}} }; } }
            """;
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void MarkupGuard_RejectsRowOverridesButAllowsBindings()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"),
            element => element.Attribute("Name")?.Value == "ValidateSharedTaskNavigationRows");
        var query = Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;
        var markup = XDocument.Parse("""
            <Grid xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppTaskNavigationRow Width="100" />
              <ui:AppTaskNavigationRow Padding="3" />
              <ui:AppTaskNavigationRow><UserControl.Content><ui:AppText /></UserControl.Content></ui:AppTaskNavigationRow>
              <ui:AppTaskNavigationRow><ui:AppText /></ui:AppTaskNavigationRow>
              <ui:AppTaskNavigationRow Title="{Binding Title}" CommandParameter="{Binding Task}" />
            </Grid>
            """);
        Assert.Equal(4, markup.XPathSelectElements(query).Count());
        var sidebar = XDocument.Load(Path.Combine(AppPath(), "Views", "WorkspaceSidebar.axaml"));
        var projectRow = Assert.Single(sidebar.Descendants(), element =>
            element.Name.LocalName == "AppProjectNavigationRow");
        Assert.DoesNotContain(projectRow.Elements(), element =>
            element.Attribute("Classes")?.Value == "sidebar-selected-surface");
    }

    private Task WithWindow(Func<MainWindow, MainWindowViewModel, Task> action) => _session.DispatchAsync(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-task-row-tests-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellation = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellation);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "Task row fixture", root, [root], now);
            var other = project with { Id = Guid.NewGuid(), Name = "Recent fixture", CreatedAt = now.AddMinutes(-1) };
            await repository.SaveProjectAsync(project, cancellation);
            await repository.SaveProjectAsync(other, cancellation);
            var selected = new AgentTask(Guid.NewGuid(), project.Id, "Open conversation", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, now, now, null);
            var target = selected with { Id = Guid.NewGuid(), Title = new string('长', 80), UpdatedAt = now.AddSeconds(-1) };
            var pinned = selected with { Id = Guid.NewGuid(), Title = "Pinned conversation", IsPinned = true };
            var recent = selected with { Id = Guid.NewGuid(), ProjectId = other.Id, Title = new string('R', 120) };
            foreach (var task in new[] { selected, target, pinned, recent })
                await repository.SaveTaskAsync(task, cancellation);
            await repository.SetTaskPinnedAsync(pinned.Id, true, cancellation);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            model.Workspace.Catalog.SelectedProject = project;
            model.Workspace.Catalog.UpsertTask(selected);
            foreach (var group in model.Workspace.Catalog.ProjectNavigation)
                group.IsExpanded = group.Project.Id == project.Id;
            var window = new MainWindow { DataContext = model, WindowState = WindowState.Normal };
            window.Show();
            try { window.UpdateLayout(); await action(window, model); }
            finally { window.Close(); }
        }
        finally { SqliteTestPool.Clear(root); Directory.Delete(root, recursive: true); }
    }, TestContext.Current.CancellationToken);

    private static void Activate(MainWindow window, AppButton button)
    {
        window.UpdateLayout();
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        window.UpdateLayout();
    }

    private static Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "src", "MossAgent.App");
    }
}
