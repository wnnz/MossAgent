using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ComposerContextBarTests
{
    [Fact]
    public void SelectedProject_UsesFullWidthComposerContextBar()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var composer = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceComposerView.axaml"));
        var component = Assert.Single(composer.Descendants(), element =>
            element.Name.LocalName == "AppComposerContextBar");
        Assert.Equal("0", component.Attribute("Grid.Row")?.Value);
        Assert.Equal("{Binding Catalog.SelectedProject}", component.Attribute("Project")?.Value);
        Assert.Equal("{Binding RepositoryStatus.BranchLabel}", component.Attribute("BranchLabel")?.Value);
        Assert.Equal("{Binding SelectedAgentLabel}", component.Attribute("AgentName")?.Value);
        Assert.Equal("{Binding HasAgents}", component.Attribute("HasAgents")?.Value);
        Assert.Equal("{Binding Agents}", component.Attribute("Agents")?.Value);
        Assert.Equal("{Binding OpenProjectPickerCommand}", component.Attribute("OpenProjectCommand")?.Value);
        Assert.Equal("{Binding SelectAgentCommand}", component.Attribute("SelectAgentCommand")?.Value);
        Assert.Null(component.Attribute("IsVisible"));
        Assert.Contains(component.Descendants(), element => element.Name.LocalName == "ProjectPickerFlyoutView");
        Assert.DoesNotContain(composer.Descendants(), element =>
            element.Attribute("Classes")?.Value == "composer-context-surface");
        var shared = XDocument.Load(Path.Combine(appPath, "Controls", "AppComposerContextBar.axaml"));
        var bar = Assert.Single(shared.Descendants(), element =>
            element.Name.LocalName == "AppBorder"
            && element.Attribute("Classes")?.Value == "composer-context-surface");

        var layout = Assert.Single(bar.Elements());
        Assert.Equal("Grid", layout.Name.LocalName);
        Assert.Equal("Auto,*,Auto", layout.Attribute("ColumnDefinitions")?.Value);
        var projectButton = Assert.Single(layout.Elements(), element =>
            element.Name.LocalName == "AppActionButton"
            && element.Attribute("ToolTip.Tip")?.Value == "{Binding ProjectLabel}");
        var details = Assert.Single(layout.Elements(), element => element.Name.LocalName == "Grid"
            && element.Attribute("Grid.Column")?.Value == "1");
        var branchLabel = Assert.Single(details.Elements(), element =>
            element.Name.LocalName == "AppIconLabel"
            && element.Attribute("IconData")?.Value == "{StaticResource UiIconBranch}");
        Assert.Equal("1", branchLabel.Attribute("Grid.Column")?.Value);
        Assert.Contains(details.Elements(), element => element.Name.LocalName == "AppContentControl"
            && element.Attribute("Content")?.Value == "{Binding #Root.EnvironmentPickerContent}");
        Assert.DoesNotContain(projectButton.Descendants(), element =>
            element.Attribute("IconData")?.Value == "{StaticResource UiIconBranch}");
        Assert.Contains(layout.Elements(), element =>
            element.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                && attribute.Value == "AgentButton")
            && element.Attribute("Grid.Column")?.Value == "2");
        Assert.Contains(bar.Descendants(), element => element.Name.LocalName == "AppFlyout");
        Assert.Contains(bar.Descendants(), element => element.Name.LocalName == "AppContentControl"
            && element.Attribute("Content")?.Value == "{Binding #Root.ProjectPickerContent}"
            && element.Attribute("DataContext")?.Value == "{Binding #Root.DataContext}");
        Assert.Equal("{Binding OpenProjectCommand}", projectButton.Attribute("Command")?.Value);
        Assert.Equal("{Binding ProjectLabel}", projectButton.Attribute("Label")?.Value);
        foreach (var icon in new[] { "UiIconFolder", "UiIconBranch" })
        {
            Assert.Contains(bar.Descendants(), element => element.Name.LocalName is "AppIconLabel" or "AppActionButton"
                && element.Attribute("IconData")?.Value == $"{{StaticResource {icon}}}");
        }
        Assert.DoesNotContain(bar.Descendants(), element => element.Name.LocalName == "AppButton"
            && element.Elements().Any(child => child.Name.LocalName == "StackPanel"
                && child.Elements().Any(grandchild => grandchild.Name.LocalName == "AppIcon")));
        Assert.DoesNotContain(bar.Descendants(), element => element.Name.LocalName == "AppBorder"
            && element.Attribute("Classes")?.Value == "divider-fill");

        AssertSharedSurfaceAppearance(appPath);
    }

    private static void AssertSharedSurfaceAppearance(string appPath)
    {
        var variants = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        var surface = Assert.Single(variants.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppBorder.composer-context-surface");
        var setters = surface.Elements().ToDictionary(
            element => element.Attribute("Property")!.Value,
            element => element.Attribute("Value")!.Value);
        Assert.Equal("38", setters["Height"]);
        Assert.Equal("12,0,12,0", setters["Margin"]);
        Assert.Equal("Stretch", setters["HorizontalAlignment"]);
        Assert.Equal("14", setters["CornerRadius"]);
        Assert.Equal("{DynamicResource CardSubtleBackgroundBrush}", setters["Background"]);
        Assert.DoesNotContain("BorderBrush", setters.Keys);

        var responsive = XDocument.Load(Path.Combine(appPath, "Styles", "ComposerResponsiveVariants.axaml"));
        var compactBar = Assert.Single(responsive.Descendants(), element =>
            element.Attribute("Selector")?.Value == "Grid.compact-composer controls|AppBorder.composer-context-surface");
        var compactSetters = compactBar.Elements().ToDictionary(
            element => element.Attribute("Property")!.Value,
            element => element.Attribute("Value")!.Value);
        Assert.Equal("15,0,15,0", compactSetters["Margin"]);
        Assert.Equal("32", compactSetters["Height"]);
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
