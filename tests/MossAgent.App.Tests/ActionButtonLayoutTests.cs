using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ActionButtonLayoutTests
{
    [Fact]
    public void LongLabel_UsesBoundedColumnWithoutReplacingBadgeAndTrailingIcon()
    {
        var button = new AppActionButton
        {
            Label = string.Concat(Enumerable.Repeat("需要截断的项目名称", 8)),
            IconData = new RectangleGeometry(),
            BadgeText = "选中",
            TrailingIconData = new RectangleGeometry()
        };
        var layout = Assert.IsType<Grid>(button.Content);
        var iconLabel = Assert.IsType<AppIconLabel>(layout.Children[0]);
        var label = Assert.IsType<AppText>(iconLabel.Children[1]);
        var badge = Assert.IsType<AppText>(layout.Children[1]);
        var trailing = Assert.IsType<AppIcon>(layout.Children[2]);

        Assert.True(badge.IsVisible);
        Assert.True(trailing.IsVisible);
        Assert.Equal(GridLength.Star, layout.ColumnDefinitions[0].Width);
        Assert.Equal(GridLength.Auto, layout.ColumnDefinitions[1].Width);
        Assert.Equal(GridLength.Auto, layout.ColumnDefinitions[2].Width);
        Assert.Equal(GridLength.Star, iconLabel.ColumnDefinitions[1].Width);
        Assert.Equal(1, Grid.GetColumn(badge));
        Assert.Equal(2, Grid.GetColumn(trailing));
        Assert.Equal(1, Grid.GetColumn(label));
        Assert.Equal(TextTrimming.CharacterEllipsis, label.TextTrimming);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Left, label.HorizontalAlignment);
        Assert.Equal(button.Label + "，选中", AutomationProperties.GetName(button));
    }

    [Fact]
    public void DropdownWithoutLeadingIcon_HasNoEmptyIconSlot()
    {
        var button = new AppActionButton { Label = "MossAgent", TrailingIconData = new RectangleGeometry() };
        var layout = Assert.IsType<Grid>(button.Content);
        var iconLabel = Assert.IsType<AppIconLabel>(layout.Children[0]);
        var leading = Assert.IsType<AppIcon>(iconLabel.Children[0]);
        var label = Assert.IsType<AppText>(iconLabel.Children[1]);

        Assert.False(leading.IsVisible);
        Assert.Equal(GridLength.Auto, iconLabel.ColumnDefinitions[0].Width);
        Assert.Equal(0, iconLabel.ColumnSpacing);
        Assert.Equal(new Thickness(), label.Margin);
        Assert.True(Assert.IsType<AppIcon>(layout.Children[2]).IsVisible);
        Assert.Equal("MossAgent", AutomationProperties.GetName(button));
    }

    [Fact]
    public void SemanticLabelLimitAndSelection_DoNotReplaceTheCommand()
    {
        var executions = 0;
        var button = new AppActionButton
        {
            Label = "项目",
            LabelMaxWidth = 160,
            TrailingIconData = new RectangleGeometry(),
            IsTrailingIconVisible = false,
            Command = new RelayCommand(() => executions++)
        };
        var layout = Assert.IsType<Grid>(button.Content);
        var iconLabel = Assert.IsType<AppIconLabel>(layout.Children[0]);
        var label = Assert.IsType<AppText>(iconLabel.Children[1]);
        var trailing = Assert.IsType<AppIcon>(layout.Children[2]);

        Assert.Equal(160, label.MaxWidth);
        Assert.False(trailing.IsVisible);
        button.LabelMaxWidth = 130;
        button.IsTrailingIconVisible = true;
        button.Label = "已选择项目";
        Assert.Equal(130, label.MaxWidth);
        Assert.True(trailing.IsVisible);
        Assert.Equal("已选择项目", AutomationProperties.GetName(button));
        Assert.Equal(0, executions);
        button.Command!.Execute(null);
        Assert.Equal(1, executions);
        button.TrailingIconData = null;
        Assert.False(trailing.IsVisible);
    }

    [Fact]
    public void SidebarAndPickers_UseSemanticActionsRatherThanPageLayouts()
    {
        var root = RepositoryRoot();
        var sidebar = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        var brand = Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("sidebar-brand-trigger") == true);
        Assert.NotNull(brand.Attribute("Label"));
        Assert.All(brand.Elements(), element => Assert.Equal("Button.Flyout", element.Name.LocalName));
        var project = Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppProjectNavigationRow");
        Assert.Equal("{Binding Project.Name}", project.Attribute("Title")?.Value);
        var component = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Controls", "AppProjectNavigationRow.axaml"));
        var projectAction = Assert.Single(component.Descendants(), element => element.Name.LocalName == "AppActionButton");
        Assert.Equal("{Binding Title}", projectAction.Attribute("Label")?.Value);
        Assert.All(projectAction.Elements(), element => Assert.Equal("Button.Flyout", element.Name.LocalName));
        var picker = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "ProjectPickerFlyoutView.axaml"));
        var option = Assert.Single(picker.Descendants(), element => element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("project-picker-row") == true
            && element.Attribute("CommandParameter")?.Value == "{Binding Project}");
        Assert.Equal("{Binding Project.Name}", option.Attribute("Label")?.Value);
        Assert.Equal("{StaticResource UiIconCheck}", option.Attribute("TrailingIconData")?.Value);
        Assert.Equal("{Binding IsSelected}", option.Attribute("IsTrailingIconVisible")?.Value);
        Assert.Equal("{Binding Project}", option.Attribute("CommandParameter")?.Value);
        var chat = Assert.Single(picker.Descendants(), element => element.Name.LocalName == "AppActionButton"
            && element.Attribute("Command")?.Value == "{Binding Catalog.NewChatCommand}");
        Assert.Equal("{Binding ProjectlessChatLabel}", chat.Attribute("Label")?.Value);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
