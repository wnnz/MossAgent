using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ShellGeometryTests
{
    [Fact]
    public void SettingsSidebar_FillsSharedFrameWithWorkspaceNavigation()
    {
        var root = RepositoryRoot();
        var settings = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views",
            "SettingsOverlay.axaml"));
        var sidebar = Assert.Single(settings.Descendants(), element =>
            element.Name.LocalName == "AppBorder"
            && element.Attribute("Classes")?.Value == "settings-sidebar-surface");
        var title = Assert.Single(sidebar.Descendants(), element =>
            element.Name.LocalName == "AppText" && element.Attribute("Classes")?.Value == "settings-sidebar-title");
        var rail = Assert.Single(settings.Descendants(), element => element.Name.LocalName == "AppNavigationRail");
        var search = Assert.Single(sidebar.Descendants(), element =>
            element.Name.LocalName == "Grid" && element.Attribute("Grid.Row")?.Value == "1");
        var window = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views",
            "MainWindow.axaml"));
        var overlay = Assert.Single(window.Descendants(), element =>
            element.Name.LocalName == "SettingsOverlay");
        var application = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "App.axaml"));
        var navigation = Assert.Single(application.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == ".app-toggle-button.settings-nav");
        Assert.Equal("AppNavigationPanes.NavigationPane", sidebar.Parent?.Name.LocalName);
        Assert.Equal("{Binding #Root.SidebarTitle}", title.Attribute("Text")?.Value);
        Assert.Equal("{Binding CloseSettingsCommand}", rail.Attribute("NewConversationCommand")?.Value);
        Assert.Equal("True", rail.Attribute("IsSettingsSelected")?.Value);
        Assert.Null(overlay.Attribute("Margin"));
        Assert.Null(sidebar.Attribute("Width"));
        Assert.Null(sidebar.Attribute("IsVisible"));
        Assert.Equal("settings-search-row", search.Attribute("Classes")?.Value);
        Assert.DoesNotContain(sidebar.Descendants(), element =>
            element.Name.LocalName == "AppIconButton");
        Assert.Contains(navigation.Elements(), element => element.Attribute("Property")?.Value == "Margin"
            && element.Attribute("Value")?.Value == "0");
    }

    [Fact]
    public void SidebarPane_UsesReferenceColorSeparateFromNavigationRail()
    {
        var root = RepositoryRoot();
        var application = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "App.axaml"));
        var light = Assert.Single(application.Descendants(), element => element.Name.LocalName == "ResourceDictionary"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "Light"));
        var brush = Assert.Single(light.Elements(), element => element.Name.LocalName == "SolidColorBrush"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "WorkspaceSidebarBackgroundBrush"));
        var styles = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles", "SurfaceVariants.axaml"));

        Assert.Equal("#F9FBFD", brush.Attribute("Color")?.Value);
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppBorder.workspace-sidebar-surface"
            && element.Descendants().Any(setter => setter.Attribute("Value")?.Value
                == "{DynamicResource WorkspaceSidebarBackgroundBrush}"));
        var sidebar = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppBorder.workspace-sidebar-surface");
        Assert.Contains(sidebar.Elements(), element => element.Attribute("Property")?.Value == "BorderBrush"
            && element.Attribute("Value")?.Value == "{DynamicResource SidebarBorderBrush}");
        Assert.Contains(sidebar.Elements(), element => element.Attribute("Property")?.Value == "BorderThickness"
            && element.Attribute("Value")?.Value == "0,0,1,0");
        Assert.Contains(sidebar.Elements(), element => element.Attribute("Property")?.Value == "CornerRadius"
            && element.Attribute("Value")?.Value == "12,0,0,0");
        var main = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppBorder.workspace-main-surface");
        Assert.DoesNotContain(main.Elements(), element => element.Attribute("Property")?.Value is "BorderBrush" or "CornerRadius");
        Assert.Contains(main.Elements(), element => element.Attribute("Property")?.Value == "BorderThickness"
            && element.Attribute("Value")?.Value == "0");
    }

    [Fact]
    public void MainWindow_DocksExpandedInspectorWithoutCoveringComposer()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "MainWindow.axaml"));
        var columns = Assert.Single(window.Descendants(), element =>
            element.Name.LocalName == "AppWorkspacePanes"
            && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                == "WorkspaceColumns");
        var chat = Assert.Single(columns.Descendants(), element => element.Name.LocalName == "WorkspaceChatView");
        var inspector = Assert.Single(columns.Descendants(), element => element.Name.LocalName == "InspectorPane");

        Assert.Equal("{Binding IsRightPanelOpen, Mode=TwoWay}", columns.Attribute("IsInspectorOpen")?.Value);
        Assert.Equal("{Binding Workspace}", chat.Attribute("DataContext")?.Value);
        Assert.Null(inspector.Attribute("Width"));
        Assert.Null(inspector.Attribute("Margin"));
        Assert.Equal("AppWorkspacePanes.PrimaryPane", chat.Parent?.Name.LocalName);
        Assert.Equal("AppWorkspacePanes.InspectorPane", inspector.Parent?.Name.LocalName);
    }

    [Fact]
    public void MainWindow_ApprovalActionsOccupySeparateColumns()
    {
        var viewsPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        var window = XDocument.Load(Path.Combine(viewsPath, "MainWindow.axaml"));
        var host = Assert.Single(window.Descendants(), element => element.Name.LocalName == "ToolApprovalView");
        Assert.Equal("{Binding Approval}", host.Attribute("DataContext")?.Value);
        var visibility = Assert.Single(host.Parent!.Elements(), element => element.Name.LocalName == "AppBorder.IsVisible");
        var conditions = Assert.Single(visibility.Elements());
        Assert.Equal("MultiBinding", conditions.Name.LocalName);
        Assert.Equal("{x:Static BoolConverters.And}", conditions.Attribute("Converter")?.Value);
        Assert.Equal(new[] { "Approval.IsPending", "!#WorkspaceNavigation.IsOverlayOpen", "!#SettingsView.Navigation.IsOverlayOpen" },
            conditions.Elements().Select(binding => binding.Attribute("Path")?.Value));
        var approval = XDocument.Load(Path.Combine(viewsPath, "ToolApprovalView.axaml"));
        var deny = Assert.Single(approval.Descendants(), element =>
            element.Name.LocalName == "AppApprovalButton"
            && element.Attribute("Command")?.Value == "{Binding DenyCommand}");
        var approve = Assert.Single(approval.Descendants(), element =>
            element.Name.LocalName == "AppApprovalButton"
            && element.Attribute("Command")?.Value == "{Binding ApproveCommand}");

        var stop = Assert.Single(approval.Descendants(), element => element.Name.LocalName == "AppApprovalButton"
            && element.Attribute("Content")?.Value == "{x:Static resources:ApprovalText.StopTask}");
        Assert.Same(stop.Parent, deny.Parent);
        Assert.Same(deny.Parent?.Parent, approve.Parent);
        Assert.Equal("Auto,*", deny.Parent?.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("*,*", approve.Parent?.Attribute("ColumnDefinitions")?.Value);
        Assert.Null(stop.Attribute("Grid.Column"));
        Assert.Equal("1", deny.Attribute("Grid.Column")?.Value);
        Assert.Equal("1", approve.Attribute("Grid.Column")?.Value);
        Assert.Equal("{Binding $parent[Window].DataContext.ApprovalTaskControl.StopCommand}", stop.Attribute("Command")?.Value);
        Assert.Equal("{Binding CurrentRequestId}", stop.Attribute("CommandParameter")?.Value);
        Assert.Equal("{Binding CurrentRequestId}", deny.Attribute("CommandParameter")?.Value);
        Assert.Equal("{Binding CurrentRequestId}", approve.Attribute("CommandParameter")?.Value);
    }

    [Fact]
    public void Composer_UsesReferenceBottomInset()
    {
        var root = RepositoryRoot();
        var composer = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceComposerView.axaml"));
        var grid = composer.Descendants().First(element => element.Name.LocalName == "Grid");

        Assert.Equal("composer-root", grid.Attribute("Classes")?.Value);
        Assert.Null(grid.Attribute("Margin"));
        Assert.Equal("736", grid.Attribute("MaxWidth")?.Value);
        var input = Assert.Single(composer.Descendants(), element =>
            element.Name.LocalName == "AppComposerInput"
            && element.Attribute("Classes")?.Value == "composer-input");
        Assert.Null(input.Attribute("MinHeight"));

        var styles = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles",
            "CardInsetVariants.axaml"));
        var inset = Assert.Single(styles.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppCard.card-inset-composer");
        Assert.Equal("14,6", Assert.Single(inset.Elements()).Attribute("Value")?.Value);

        var responsive = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles",
            "ComposerResponsiveVariants.axaml"));
        foreach (var (selector, expected) in new[]
        {
            ("Grid.composer-root", "24,0,28,16"),
            ("Grid.composer-root.compact-composer", "15,0,19,20"),
            ("Grid.compact-composer controls|AppCard.card-inset-composer", "14,0,14,6")
        })
        {
            var style = Assert.Single(responsive.Descendants(), element =>
                element.Attribute("Selector")?.Value == selector);
            Assert.Equal(expected, Assert.Single(style.Elements()).Attribute("Value")?.Value);
        }
        Assert.Contains(responsive.Descendants(), element => element.Attribute("Property")?.Value == "Height"
            && element.Attribute("Value")?.Value == "32");
        Assert.Contains(responsive.Descendants(), element => element.Attribute("Property")?.Value == "MinHeight"
            && element.Attribute("Value")?.Value == "36");
    }

    [Fact]
    public void Composer_ModelPickerTruncatesWithinSharedWidth()
    {
        var composer = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "WorkspaceComposerView.axaml"));
        var picker = Assert.Single(composer.Descendants(), element =>
            element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("composer-model-picker") == true);
        var variants = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "ActionButtonVariants.axaml"));

        Assert.Equal("{Binding ModelPicker.ComposerDisplayName}", picker.Attribute("Label")?.Value);
        Assert.DoesNotContain(picker.Elements(), element => element.Name.LocalName == "Grid");
        Assert.Equal("Auto,Auto,*,Auto", picker.Parent?.Attribute("ColumnDefinitions")?.Value);
        Assert.Equal("2", picker.Attribute("Grid.Column")?.Value);
        Assert.Null(picker.Attribute("HorizontalAlignment"));
        Assert.Contains(picker.Parent!.Elements(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Classes")?.Value.Contains("composer-send", StringComparison.Ordinal) == true
            && element.Attribute("Grid.Column")?.Value == "3");
        var pickerStyle = Assert.Single(variants.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppButton.composer-model-picker");
        Assert.Contains(pickerStyle.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "MaxWidth"
            && element.Attribute("Value")?.Value == "320");
        Assert.Contains(pickerStyle.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "HorizontalAlignment"
            && element.Attribute("Value")?.Value == "Right");
    }

    [Fact]
    public void SidebarTaskRows_UseReferenceInsetsAndSelectionColor()
    {
        var root = RepositoryRoot();
        var sidebar = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        var projectRow = sidebar.Descendants().Single(element => element.Name.LocalName == "AppProjectNavigationRow");
        var projectComponent = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Controls", "AppProjectNavigationRow.axaml"));
        var taskList = sidebar.Descendants().Single(element => element.Name.LocalName == "AppItemsControl"
            && element.Attribute("ItemsSource")?.Value == "{Binding TaskPreview.Items}");
        var taskRow = taskList.Descendants().Single(element => element.Name.LocalName == "AppTaskNavigationRow");
        var component = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Controls", "AppTaskNavigationRow.axaml"));
        var projectLabel = projectComponent.Descendants().Single(element => element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("sidebar-project-row") == true);
        var taskLabel = component.Descendants().Single(element => element.Name.LocalName == "AppText"
            && element.Attribute("Classes")?.Value == "sidebar-row");
        var selectedBackground = component.Descendants().First(element => element.Name.LocalName == "AppBorder"
            && element.Attribute("IsVisible")?.Value == "{Binding IsSelected}");

        var navigation = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles", "NavigationActionVariants.axaml"));
        Assert.Contains(navigation.Descendants(), element => element.Attribute("Selector")?.Value == ".app-project-navigation-row"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "Margin"
                && setter.Attribute("Value")?.Value == "0,0,10,0"));
        Assert.Equal("{Binding IsSelected}", projectRow.Attribute("IsSelected")?.Value);
        Assert.DoesNotContain(projectRow.Elements(), element => element.Name.LocalName == "AppBorder"
            && element.Attribute("Classes")?.Value == "sidebar-selected-surface");
        Assert.Equal("sidebar-task-list", taskList.Attribute("Classes")?.Value);
        Assert.Contains(navigation.Descendants(), element => element.Attribute("Selector")?.Value == ".sidebar-task-list"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "Margin"
                && setter.Attribute("Value")?.Value == "0,0,10,0"));
        Assert.Equal("{Binding IsSelected}", taskRow.Attribute("IsSelected")?.Value);
        Assert.Equal("True", taskRow.Attribute("IsProjectTask")?.Value);
        Assert.Equal("{Binding Project.Name}", projectRow.Attribute("Title")?.Value);
        Assert.Equal("{Binding Title}", projectLabel.Attribute("Label")?.Value);
        Assert.Equal("sidebar-row", taskLabel.Attribute("Classes")?.Value);
        Assert.Null(projectLabel.Attribute("FontSize"));
        Assert.Null(taskLabel.Attribute("FontSize"));
        Assert.Equal("sidebar-selected-surface", selectedBackground.Attribute("Classes")?.Value);
        var variants = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles",
            "SurfaceVariants.axaml"));
        var selectionStyle = Assert.Single(variants.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppBorder.sidebar-selected-surface");
        Assert.Contains(selectionStyle.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Background"
            && element.Attribute("Value")?.Value == "{DynamicResource SettingsSelectionBrush}");
    }

    [Fact]
    public void EmptyConversation_UsesDedicatedIconAndOnlyDividesActiveTaskHeader()
    {
        var root = RepositoryRoot();
        var chat = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceChatView.axaml"));
        var resources = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "App.axaml"));
        var variants = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles", "SurfaceVariants.axaml"));
        var header = Assert.Single(XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Controls",
            "AppConversationHeader.axaml")).Descendants(), element => element.Name.LocalName == "AppBorder"
            && element.Attribute("Classes")?.Value == "chat-header");
        var emptyIcon = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "AppIcon"
            && element.Attribute("Data")?.Value == "{StaticResource UiIconEmptyAgent}");
        var welcomeLayout = emptyIcon.Parent;

        Assert.Equal("AppEmptySessionLayout", welcomeLayout?.Name.LocalName);
        Assert.Null(welcomeLayout?.Attribute("Margin"));
        Assert.Null(welcomeLayout?.Attribute("BrandOffset"));
        Assert.Null(emptyIcon.Attribute("RenderTransform"));
        Assert.Contains("empty-agent-offset", emptyIcon.Attribute("Classes")?.Value.Split(' ') ?? []);
        Assert.Contains(variants.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppEmptySessionLayout"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "BrandOffset"
                && setter.Attribute("Value")?.Value == "32"));
        Assert.Equal("{Binding #Root.HasConversation}",
            header.Attribute("Classes.active")?.Value);
        Assert.Contains(resources.Descendants(), element => element.Name.LocalName == "StreamGeometry"
            && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value
                == "UiIconEmptyAgent");
        Assert.Contains(variants.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppBorder.chat-header.active");
    }

    [Fact]
    public void Conversation_UsesMeasuredContextCardHeightForMessageInset()
    {
        var root = RepositoryRoot();
        var chat = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceChatView.axaml"));
        var inspector = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "InspectorPane.axaml"));

        var messages = Assert.Single(chat.Descendants(), element =>
            element.Name.LocalName == "AppScrollViewer"
            && element.Attribute("IsVisible")?.Value == "{Binding HasMessages}");
        var emptySession = Assert.Single(chat.Descendants(), element =>
            element.Name.LocalName == "Grid"
            && element.Attribute("IsVisible")?.Value == "{Binding IsEmptySession}");
        var card = Assert.Single(inspector.Descendants(), element =>
            element.Name.LocalName == "AppCard"
            && element.Attribute("Classes")?.Value == "inspector-surface card-inset-inspector");
        var branchLabel = Assert.Single(card.Descendants(), element => element.Name.LocalName == "AppText"
            && element.Attribute("Text")?.Value == "{Binding Workspace.RepositoryStatus.BranchLabel}");

        Assert.Equal("{Binding $parent[Window].DataContext.ChatMessageViewportMargin}",
            messages.Attribute("Margin")?.Value);
        var emptyMargin = Assert.Single(emptySession.Elements(), element => element.Name.LocalName == "Grid.Margin");
        var marginBinding = Assert.Single(emptyMargin.Elements(), element => element.Name.LocalName == "MultiBinding");
        Assert.Equal("{StaticResource EmptySessionViewportMarginConverter}", marginBinding.Attribute("Converter")?.Value);
        Assert.Equal(["Catalog.SelectedTask", "$parent[Window].DataContext.ChatMessageViewportMargin"],
            marginBinding.Elements().Select(element => element.Attribute("Path")?.Value));
        Assert.Equal("28,24", messages.Attribute("Padding")?.Value);
        Assert.Equal("HandleContextCardSizeChanged", card.Attribute("SizeChanged")?.Value);
        Assert.Equal("{Binding Workspace.Catalog.SelectedTask, Converter={x:Static ObjectConverters.IsNotNull}}",
            card.Parent?.Attribute("IsVisible")?.Value);
        Assert.Contains(branchLabel.Parent!.Elements(), element => element.Name.LocalName == "AppIcon"
            && element.Attribute("Data")?.Value == "{StaticResource UiIconBranch}");
    }

    [Fact]
    public void ChatHeader_MoreMenuAppearsOnlyForActiveTask()
    {
        var chat = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "WorkspaceChatView.axaml"));
        var more = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "AppConversationHeader");
        var options = more.Descendants().Where(element => element.Name.LocalName == "AppButton").ToArray();

        Assert.Equal("{Binding Catalog.SelectedTask}", more.Attribute("ConversationTarget")?.Value);
        Assert.Contains(options, option => option.Attribute("Content")?.Value == "新对话"
            && option.Attribute("Command")?.Value == "{Binding Catalog.NewTaskCommand}");
        Assert.Contains(options, option => option.Attribute("Command")?.Value
            == "{Binding Catalog.TaskManagement.TogglePinCommand}"
            && option.Attribute("Content")?.Value == "{Binding Catalog.TaskManagement.PinToggleLabel}");
        Assert.Equal(2, options.Count(option => option.Attribute("IsVisible")?.Value
            == "{Binding Catalog.SelectedTask, Converter={x:Static ObjectConverters.IsNotNull}}"));
    }

    [Fact]
    public void BrowserSettings_AlignsPrimaryCardAndGeneralSection()
    {
        var browser = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "BrowserSettingsPane.axaml"));
        var content = Assert.Single(browser.Descendants(), element =>
            element.Name.LocalName == "StackPanel" && element.Attribute("Margin")?.Value == "0,0,0,24");
        var primaryCard = Assert.Single(content.Elements(), element => element.Name.LocalName == "AppCard"
            && element.Descendants().Any(child => child.Name.LocalName == "AppComboBox"
                && child.Attribute("SelectedItem")?.Value == "{Binding SelectedBrowserKind}"));
        var general = Assert.Single(content.Elements(), element => element.Name.LocalName == "StackPanel"
            && element.Elements().Any(child => child.Name.LocalName == "AppText"
                && child.Attribute("Text")?.Value == "常规"));

        Assert.Equal("0,11,0,0", primaryCard.Attribute("Margin")?.Value);
        Assert.Contains("card-inset-browser-intro", primaryCard.Attribute("Classes")?.Value.Split(' ') ?? []);
        var insets = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "CardInsetVariants.axaml"));
        var introStyle = Assert.Single(insets.Descendants(), element =>
            element.Attribute("Selector")?.Value == "controls|AppCard.card-inset-browser-intro");
        Assert.Contains(introStyle.Elements(), element => element.Attribute("Property")?.Value == "Padding"
            && element.Attribute("Value")?.Value == "16,14");
        Assert.Equal("0,35,0,0", general.Attribute("Margin")?.Value);
        var proxySelector = Assert.Single(general.Descendants(), element =>
            element.Name.LocalName == "AppComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedProxy}");
        Assert.Null(proxySelector.Attribute("HorizontalAlignment"));
        Assert.Equal("AppSettingsRow", proxySelector.Parent?.Name.LocalName);
        var rows = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SettingsRowVariants.axaml"));
        var rowStyle = Assert.Single(rows.Descendants(), element =>
            element.Attribute("Selector")?.Value == "ui|AppSettingsRow > .app-combo-box, ui|AppSettingsRow > .app-input");
        Assert.Contains(rowStyle.Elements(), element => element.Attribute("Property")?.Value == "HorizontalAlignment"
            && element.Attribute("Value")?.Value == "Stretch");
    }

    [Fact]
    public void SettingsContent_UsesResponsiveWidthAndFullPaneModals()
    {
        var settings = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "SettingsOverlay.axaml"));
        var surface = Assert.Single(settings.Root!.Elements(), element =>
            element.Name.LocalName == "AppBorder" && element.Attribute("Classes")?.Value == "settings-shell-surface");
        var variants = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SurfaceVariants.axaml"));
        var shellStyle = Assert.Single(variants.Descendants(), element =>
            element.Attribute("Selector")?.Value == "controls|AppBorder.settings-shell-surface");
        Assert.Contains(shellStyle.Elements(), element => element.Attribute("Property")?.Value == "Background"
            && element.Attribute("Value")?.Value == "{DynamicResource HeaderBackgroundBrush}");
        var shell = Assert.Single(surface.Elements(), element => element.Name.LocalName == "Grid");
        var content = Assert.Single(settings.Descendants(), element =>
            element.Name.LocalName == "Grid"
            && element.Attribute("Classes")?.Value == "settings-content-column");
        var providerPane = Assert.Single(settings.Descendants(), element =>
            element.Name.LocalName == "ProviderAndModelSettingsPane");
        var providerSettings = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "ProviderAndModelSettingsPane.axaml"));
        var rootGrid = Assert.Single(providerSettings.Root!.Elements(), element =>
            element.Name.LocalName == "Grid");
        var providerContent = Assert.Single(rootGrid.Elements(), element =>
            element.Name.LocalName == "Grid"
            && element.Attribute("Classes")?.Value == "settings-content-column");
        var modals = rootGrid.Elements().Where(element =>
            element.Name.LocalName == "AppSettingsDialog"
            && element.Attribute("IsOpen")?.Value.EndsWith(".IsFormOpen}", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal("Auto,*", shell.Attribute("ColumnDefinitions")?.Value);
        var sidebar = Assert.Single(shell.Descendants(), element =>
            element.Attribute("Classes")?.Value == "settings-sidebar-surface");
        var page = Assert.Single(shell.Descendants(), element =>
            element.Attribute("Classes")?.Value == "page-surface settings-page-surface");
        Assert.Equal("AppNavigationPanes.NavigationPane", sidebar.Parent?.Name.LocalName);
        Assert.Equal("AppNavigationPanes.PrimaryPane", page.Parent?.Name.LocalName);
        Assert.Same(sidebar.Parent!.Parent, page.Parent!.Parent);
        Assert.Contains(variants.Descendants(), element =>
            element.Attribute("Selector")?.Value == "controls|AppBorder.settings-page-surface"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "CornerRadius"
                && setter.Attribute("Value")?.Value == "0"));
        Assert.Null(content.Attribute("Margin"));
        Assert.Null(providerContent.Attribute("Margin"));
        var layoutStyles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SettingsLayoutVariants.axaml"));
        var columnStyle = Assert.Single(layoutStyles.Descendants(), element =>
            element.Attribute("Selector")?.Value == "Grid.settings-content-column");
        Assert.Contains(columnStyle.Elements(), setter => setter.Attribute("Property")?.Value == "MaxWidth"
            && setter.Attribute("Value")?.Value == "728");
        Assert.Contains(columnStyle.Elements(), setter => setter.Attribute("Property")?.Value == "Margin"
            && setter.Attribute("Value")?.Value == "28,74,38,34");
        Assert.Equal("Grid", providerPane.Parent?.Name.LocalName);
        Assert.NotSame(content, providerPane.Parent);
        Assert.Equal(2, modals.Length);
        Assert.All(modals, modal =>
        {
            Assert.Null(modal.Attribute("Grid.RowSpan"));
            Assert.Equal("wide", modal.Attribute("Classes")?.Value);
            Assert.NotNull(modal.Attribute("CloseCommand"));
            var footer = Assert.Single(modal.Elements(), element =>
                element.Name.LocalName == "AppSettingsDialog.Footer");
            Assert.Contains(footer.Descendants(), element => element.Name.LocalName == "AppButton"
                && element.Attribute("Content")?.Value.StartsWith("保存", StringComparison.Ordinal) == true);
        });
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
