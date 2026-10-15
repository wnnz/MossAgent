using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTabComponentTests
{
    [Fact]
    public void ConversationHeader_UsesFunctionalTabsAndContextualActions()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var chat = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceChatView.axaml"));
        var tabView = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceConversationTabsView.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "ConversationTabVariants.axaml"));
        var resources = XDocument.Load(Path.Combine(appPath, "App.axaml"));

        Assert.Contains(chat.Descendants(), element => element.Name.LocalName == "WorkspaceConversationTabsView");
        var tabs = Assert.Single(tabView.Descendants(), element => element.Name.LocalName == "AppItemsControl"
            && element.Attribute("ItemsSource")?.Value == "{Binding Tabs.Items}");
        var tab = Assert.Single(tabs.Descendants(), element => element.Name.LocalName == "AppConversationTab");
        Assert.Equal("{Binding Title}", tab.Attribute("Title")?.Value);
        Assert.Equal("{Binding $parent[Window].DataContext.Workspace.Tabs.SelectTabCommand}",
            tab.Attribute("SelectCommand")?.Value);
        Assert.Equal("{Binding $parent[Window].DataContext.Workspace.Tabs.CloseTabCommand}", tab.Attribute("CloseCommand")?.Value);
        Assert.Empty(tab.Elements());
        var menu = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "AppConversationHeader");
        Assert.Equal("{Binding Catalog.SelectedTask}", menu.Attribute("ConversationTarget")?.Value);
        Assert.Contains(menu.Descendants(), element => element.Attribute("Content")?.Value == "归档任务");
        var newTask = Assert.Single(tabView.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Label")?.Value == "新对话");
        Assert.Equal("{Binding Catalog.NewTaskCommand}", newTask.Attribute("Command")?.Value);
        Assert.Equal("{StaticResource UiIconPlus}", newTask.Attribute("IconData")?.Value);
        Assert.Equal("{Binding Catalog.SelectedProject, Converter={x:Static ObjectConverters.IsNotNull}}",
            newTask.Attribute("IsEnabled")?.Value);

        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppButton.conversation-tab.selected");
        Assert.Contains(resources.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/ConversationTabVariants.axaml");
        Assert.Contains(resources.Descendants(), element => element.Name.LocalName == "StreamGeometry"
            && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                == "UiIconConversation");
    }

    [Fact]
    public void SummaryIsTaskScoped_AndBottomPanelRemainsAvailableFromViewMenu()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var chat = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceChatView.axaml"));
        var main = XDocument.Load(Path.Combine(appPath, "Views", "MainWindow.axaml"));
        var summary = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "AppConversationHeader");
        Assert.Equal("{Binding Catalog.SelectedTask}", summary.Attribute("ConversationTarget")?.Value);
        Assert.Equal("{Binding $parent[Window].DataContext.ToggleContextCardCommand}",
            summary.Attribute("ToggleContextCommand")?.Value);
        Assert.DoesNotContain(chat.Descendants(), element => element.Attribute("Label")?.Value == "切换底部终端面板");

        var viewMenu = Assert.Single(main.Descendants(), element => element.Name.LocalName == "AppButton"
            && element.Attribute("Content")?.Value == "视图");
        Assert.Contains(viewMenu.Descendants(), element => element.Attribute("Command")?.Value
            == "{Binding ToggleBottomPanelCommand}");
    }

    [Fact]
    public void SidebarHeader_OrdersAuditBeforeSearchLikeReference()
    {
        var sidebar = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "WorkspaceSidebar.axaml"));
        var audit = Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Label")?.Value == "审计与执行记录");
        var search = Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppSidebarSearch");

        Assert.Equal("1", audit.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", search.Attribute("Grid.Column")?.Value);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
