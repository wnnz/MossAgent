using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppSettingsDialogTests
{
    [Fact]
    public void OpenStateControlsVisibilityAndEscapeInvokesCloseCommand()
    {
        var dialog = new AppSettingsDialog();
        Assert.False(dialog.IsVisible);
        Assert.False(dialog.IsOpen);
        Assert.False(string.IsNullOrWhiteSpace(dialog.CloseLabel));
        dialog.CloseCommand = new RelayCommand(() => dialog.IsOpen = false);
        dialog.IsOpen = true;
        Assert.True(dialog.IsVisible);

        var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };
        dialog.RaiseEvent(key);

        Assert.True(key.Handled);
        Assert.False(dialog.IsOpen);
        Assert.False(dialog.IsVisible);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void EscapeDoesNotBypassDisabledCommandsOrChildHandling(bool canClose, bool handled)
    {
        var closed = false;
        var dialog = new AppSettingsDialog { IsOpen = true };
        dialog.CloseCommand = new RelayCommand(() => closed = true, () => canClose);
        dialog.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Escape,
            Handled = handled
        });

        Assert.False(closed);
        Assert.True(dialog.IsOpen);
    }

    [Fact]
    public void SettingsEditorsUseOneSharedDialogWithSeparateBodyAndFooter()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        var dialogs = new[] { "ProxySettingsPane", "ProviderAndModelSettingsPane", "McpSettingsPane", "AgentSettingsPane", "SubagentSettingsPane" }
            .SelectMany(page => XDocument.Load(Path.Combine(views, $"{page}.axaml")).Descendants()
                .Where(element => element.Name.LocalName == "AppSettingsDialog")).ToArray();

        Assert.Equal(6, dialogs.Length);
        Assert.All(dialogs, dialog =>
        {
            Assert.NotNull(dialog.Attribute("Title"));
            Assert.NotNull(dialog.Attribute("IsOpen"));
            Assert.NotNull(dialog.Attribute("CloseCommand"));
            Assert.Contains(dialog.Elements(), element => element.Name.LocalName == "AppSettingsDialog.Footer");
            Assert.DoesNotContain(dialog.Descendants(), element => element.Name.LocalName is "AppCard" or "AppScrollViewer");
        });

        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SettingsDialogVariants.axaml"));
        var scroll = Assert.Single(styles.Descendants(), element => element.Name.LocalName == "AppScrollViewer");
        Assert.Equal("1", scroll.Attribute("Grid.Row")?.Value);
        Assert.Equal("Auto", scroll.Attribute("VerticalScrollBarVisibility")?.Value);
        Assert.Equal("Disabled", scroll.Attribute("HorizontalScrollBarVisibility")?.Value);
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "AppContentControl"
            && element.Attribute("Grid.Row")?.Value == "2" && element.Attribute("Content")?.Value == "{TemplateBinding Footer}");
        Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "AppCard"
            && element.Attribute("KeyboardNavigation.TabNavigation")?.Value == "Cycle"
            && element.Attribute("MaxWidth")?.Value.Contains("DialogAvailableExtent", StringComparison.Ordinal) == true
            && element.Attribute("MaxHeight")?.Value.Contains("DialogAvailableExtent", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("ProxySettingsPane")]
    [InlineData("McpSettingsPane")]
    [InlineData("ProviderAndModelSettingsPane")]
    [InlineData("AgentSettingsPane")]
    [InlineData("SubagentSettingsPane")]
    public void DialogsFillTheSettingsViewportInsteadOfTheContentColumn(string page)
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        var shell = XDocument.Load(Path.Combine(views, "SettingsOverlay.axaml"));
        var pane = Assert.Single(shell.Descendants(), element => element.Name.LocalName == page);
        if (page != "ProviderAndModelSettingsPane")
        {
            Assert.Null(pane.Attribute("IsVisible"));
            Assert.NotNull(pane.Parent?.Attribute("IsVisible"));
            Assert.Equal("AppBorder", pane.Parent?.Name.LocalName);
        }
        Assert.DoesNotContain(pane.Ancestors(), element => element.Attribute("Classes")?.Value == "settings-content-column");
        var markup = XDocument.Load(Path.Combine(views, $"{page}.axaml"));
        var root = Assert.Single(markup.Root!.Elements(), element => element.Name.LocalName == "Grid");
        Assert.Contains(root.Elements(), element => element.Attribute("Classes")?.Value == "settings-content-column");
        Assert.Contains(root.Elements(), element => element.Name.LocalName == "AppSettingsDialog");
    }

    [Fact]
    public void BuildGuardRejectsPageOwnedDialogsAndAppearanceBypasses()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedSettingsDialogs");
        var queries = target.Elements().Where(element => element.Name.LocalName == "XmlPeek")
            .Select(element => element.Attribute("Query")!.Value).ToArray();
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var sample = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppBorder Classes="overlay-scrim extra" />
              <ui:AppSettingsDialog Width="500" />
              <ui:AppSettingsDialog><ContentControl.Padding>12</ContentControl.Padding></ui:AppSettingsDialog>
              <ui:AppSettingsDialog><ui:AppSettingsDialog.Width>500</ui:AppSettingsDialog.Width></ui:AppSettingsDialog>
              <ui:AppSettingsDialog Title="Allowed" Classes="wide"><ui:AppSettingsDialog.Footer><ui:AppButton /></ui:AppSettingsDialog.Footer></ui:AppSettingsDialog>
            </UserControl>
            """);

        Assert.Single(sample.XPathSelectElements(queries[0]));
        Assert.Equal(3, sample.XPathSelectElements(queries[1]).Count());
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
