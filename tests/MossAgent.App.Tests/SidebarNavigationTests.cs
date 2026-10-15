using System.Xml.Linq;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarNavigationTests
{
    [Fact]
    public void ProjectsSection_TogglesWithoutLosingItsState()
    {
        var sections = new WorkspaceSidebarSectionViewModel();

        Assert.True(sections.IsProjectsExpanded);
        sections.ToggleProjectsCommand.Execute(null);
        Assert.False(sections.IsProjectsExpanded);
        sections.ExpandProjects();
        Assert.True(sections.IsProjectsExpanded);
        sections.ToggleProjectsCommand.Execute(null);
        Assert.False(sections.IsProjectsExpanded);
    }

    [Fact]
    public void ProjectSelectionAndSearch_ReopenCollapsedSection()
    {
        var repository = new TestWorkspaceRepository();
        var catalog = new WorkspaceCatalogViewModel(
            repository,
            new WorkspaceWorktreeCleanupService(repository, null!),
            new WorkspaceProjectService(repository, new TestFolderPickerService()));

        catalog.Sections.ToggleProjectsCommand.Execute(null);
        catalog.TaskSearch.Query = "review";
        Assert.True(catalog.Sections.IsProjectsExpanded);

        catalog.Sections.ToggleProjectsCommand.Execute(null);
        catalog.SelectedProject = new ProjectProfile(Guid.NewGuid(), "MossAgent",
            "D:\\Dev\\MossAgent", ["D:\\Dev\\MossAgent"], DateTimeOffset.UtcNow);
        Assert.True(catalog.Sections.IsProjectsExpanded);
    }

    [Fact]
    public void ProjectsHeader_RevealsSharedActionsOnHoverAndKeyboardFocus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(
                   Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var document = XDocument.Load(Path.Combine(directory.FullName, "src",
            "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        var header = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "AppBorder"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("projects-section-header") == true);
        var layout = Assert.Single(header.Elements(), element => element.Name.LocalName == "Grid");
        var actions = layout.Elements().Where(element => element.Name.LocalName is "AppButton" or "AppIconButton").ToArray();
        var sharedStyles = XDocument.Load(Path.Combine(directory.FullName, "src",
            "MossAgent.App", "Styles", "NavigationActionVariants.axaml"));
        var selectors = sharedStyles.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .SelectMany(element => (element.Attribute("Selector")?.Value ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        var projectList = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "AppItemsControl"
            && element.Attribute("Classes")?.Value == "sidebar-project-list");

        Assert.Null(header.Attribute("Margin"));
        Assert.Contains(sharedStyles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppBorder.projects-section-header" && element.Elements().Any(setter =>
                setter.Attribute("Property")?.Value == "Margin" && setter.Attribute("Value")?.Value == "8,0,10,5"));
        Assert.Equal(["1", "3", "4"], actions.Select(action => action.Attribute("Grid.Column")?.Value));
        Assert.All(actions, action => Assert.Contains("project-header-action",
            action.Attribute("Classes")?.Value.Split(' ') ?? []));
        Assert.Contains(actions, action => action.Attribute("Command")?.Value
            == "{Binding Sections.ToggleProjectsCommand}");
        Assert.Contains("controls|AppBorder.projects-section-header controls|AppButton.project-header-action", selectors);
        Assert.Contains("controls|AppBorder.projects-section-header:pointerover controls|AppButton.project-header-action", selectors);
        Assert.Contains("controls|AppBorder.projects-section-header:focus-within controls|AppButton.project-header-action", selectors);
        Assert.Equal("{Binding Sections.IsProjectsExpanded}", projectList.Attribute("IsVisible")?.Value);
    }

    [Fact]
    public void NavigationRailScheduledTasksEntry_UsesSuppliedCommandAndSelection()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(
                   Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "src",
            "MossAgent.App", "Controls", "AppNavigationRail.axaml");
        var document = XDocument.Load(path);
        var button = Assert.Single(document.Descendants(),
            element => element.Name.LocalName == "AppIconButton"
                && element.Attribute("Label")?.Value == "{Binding AutomationsLabel}");

        Assert.Null(button.Attribute("IsEnabled"));
        Assert.Equal("{Binding AutomationsCommand}", button.Attribute("Command")?.Value);
        Assert.Null(button.Attribute("CommandParameter"));
        Assert.Null(button.Attribute("Classes.selected"));
    }

    [Fact]
    public void ProjectRow_OpensEditableProjectSummary()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(
                   Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var document = XDocument.Load(Path.Combine(directory.FullName, "src",
            "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        var projectRow = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "AppProjectNavigationRow");
        var component = XDocument.Load(Path.Combine(directory.FullName, "src",
            "MossAgent.App", "Controls", "AppProjectNavigationRow.axaml"));
        var flyout = Assert.Single(component.Descendants(), element =>
            element.Name.LocalName == "AppFlyout");
        Assert.Equal("RightEdgeAlignedTop", flyout.Attribute("Placement")?.Value);
        Assert.Equal("{Binding IsSummaryOpen, Mode=TwoWay}", projectRow.Attribute("IsSummaryOpen")?.Value);
        Assert.Contains(projectRow.Descendants(), element =>
            element.Name.LocalName == "AppActionButton"
            && element.Attribute("Command")?.Value?.EndsWith("EditProjectCommand}") == true);
    }

    [Fact]
    public void PinnedSectionUsesSharedControlsAndRealToggleCommand()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var document = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Views", "WorkspaceSidebar.axaml"));
        var pinned = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppItemsControl"
            && element.Attribute("ItemsSource")?.Value == "{Binding TaskManagement.PinnedTasks.Items}");
        var section = Assert.Single(pinned.Ancestors(), element => element.Name.LocalName == "StackPanel"
            && element.Attribute("Classes")?.Value == "sidebar-pinned-section");
        Assert.Equal("{Binding TaskManagement.PinnedTasks.HasItems}", section.Attribute("IsVisible")?.Value);
        Assert.Contains(pinned.Descendants(), element => element.Name.LocalName == "AppTaskNavigationRow"
            && element.Attribute("TogglePinCommand")?.Value?.EndsWith("TogglePinCommand}") == true);

        var taskRows = document.Descendants().Where(element => element.Name.LocalName == "AppTaskNavigationRow").ToArray();
        Assert.Equal(3, taskRows.Length);
        Assert.All(taskRows, row =>
        {
            Assert.Equal("{Binding Task}", row.Attribute("CommandParameter")?.Value);
            Assert.Equal("{Binding PinToggleLabel}", row.Attribute("PinLabel")?.Value);
            Assert.NotNull(row.Attribute("RenameCommand"));
            Assert.NotNull(row.Attribute("ArchiveCommand"));
            Assert.NotNull(row.Attribute("SelectCommand"));
        });
    }
}
