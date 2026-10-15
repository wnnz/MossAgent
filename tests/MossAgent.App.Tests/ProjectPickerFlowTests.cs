using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectPickerFlowTests
{
    [Fact]
    public async Task SearchAndSelection_UseConfiguredProjects()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var now = DateTimeOffset.UtcNow;
            var alpha = new ProjectProfile(Guid.NewGuid(), "Alpha", root, [root], now);
            var beta = new ProjectProfile(Guid.NewGuid(), "Beta", root, [root], now);
            await repository.SaveProjectAsync(alpha, cancellationToken);
            await repository.SaveProjectAsync(beta, cancellationToken);

            var workspace = services.GetRequiredService<WorkspaceViewModel>();
            await workspace.LoadAsync();
            workspace.OpenProjectPickerCommand.Execute(null);
            Assert.Equal(2, workspace.ProjectPickerProjects.Count);
            Assert.Single(workspace.ProjectPickerProjects, group => group.IsSelected);

            workspace.ProjectPickerQuery = "bEt";
            var match = Assert.Single(workspace.ProjectPickerProjects);
            Assert.Equal(beta.Id, match.Project.Id);
            Assert.True(workspace.ProjectPickerHasMatches);
            workspace.Catalog.SelectProjectCommand.Execute(match.Project);
            Assert.Equal(beta.Id, workspace.Catalog.SelectedProject?.Id);
            Assert.True(match.IsSelected);

            workspace.ProjectPickerQuery = "unknown";
            Assert.Empty(workspace.ProjectPickerProjects);
            Assert.False(workspace.ProjectPickerHasMatches);
            workspace.OpenProjectPickerCommand.Execute(null);
            Assert.Equal(string.Empty, workspace.ProjectPickerQuery);
            Assert.Equal(2, workspace.ProjectPickerProjects.Count);
            Assert.Equal("搜索项目", workspace.ProjectPickerSearchPlaceholder);
            Assert.Equal("新建项目", workspace.ProjectPickerCreateLabel);
            Assert.Equal("切换项目", workspace.ProjectPickerSwitchLabel);
            workspace.Catalog.ProjectEditor.OpenCommand.Execute(null);
            Assert.True(workspace.Catalog.ProjectEditor.IsOpen);
        }
        finally
        {
            await DeleteWhenUnlockedAsync(root);
        }
    }

    [Fact]
    public void EmptyHeroAndComposer_ReuseSearchableProjectPicker()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var chat = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceChatView.axaml"));
        var composer = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceComposerView.axaml"));
        var picker = XDocument.Load(Path.Combine(appPath, "Views", "ProjectPickerFlyoutView.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "ProjectPickerVariants.axaml"));

        var hero = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "AppProjectPrompt");
        Assert.Equal("{Binding OpenProjectPickerCommand}", hero.Attribute("SelectProjectCommand")?.Value);
        Assert.Equal("{Binding Catalog.SelectedProject.Name}", hero.Attribute("ProjectName")?.Value);
        Assert.Contains(hero.Descendants(), element => element.Name.LocalName == "ProjectPickerFlyoutView");
        Assert.Contains(composer.Descendants(), element => element.Name.LocalName == "ProjectPickerFlyoutView");
        Assert.Contains(picker.Descendants(), element => element.Name.LocalName == "AppInput"
            && element.Attribute("Text")?.Value == "{Binding ProjectPickerQuery, Mode=TwoWay}");
        Assert.Contains(picker.Descendants(), element => element.Name.LocalName == "AppItemsControl"
            && element.Attribute("ItemsSource")?.Value == "{Binding ProjectPickerProjects}");
        Assert.Contains(picker.Descendants(), element => element.Attribute("Command")?.Value?.Contains(
            "SelectProjectCommand", StringComparison.Ordinal) == true);
        Assert.Contains(picker.Descendants(), element => element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes.selected")?.Value == "{Binding IsSelected}");
        Assert.Contains(picker.Descendants(), element => element.Attribute("Click")?.Value
            == "HandleCreateProjectClick" && element.Attribute("Command") is null);
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppText.empty-project-name"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "TextDecorations"
                && setter.Descendants().Any(decoration => decoration.Name.LocalName == "TextDecoration"
                    && decoration.Attribute("Location")?.Value == "Underline"))
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "Foreground"
                && setter.Attribute("Value")?.Value == "{DynamicResource TextSubtleBrush}"));
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppButton.project-picker-row.selected"
            && element.Elements().Any(setter => setter.Attribute("Value")?.Value
                == "{DynamicResource SettingsSelectionBrush}"));
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

    private static async Task DeleteWhenUnlockedAsync(string root)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            SqliteTestPool.Clear(root);
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                await Task.Delay(50);
            }
        }
    }
}
