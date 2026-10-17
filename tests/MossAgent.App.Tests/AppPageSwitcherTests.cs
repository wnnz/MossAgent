using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppPageSwitcherTests
{
    [Fact]
    public void OnlyTheActivePresenterIsVisibleAndPagesAreRetained()
    {
        var draft = new AppInput { Text = "unsent draft" };
        var editor = new AppInput { Text = "unsaved settings" };
        var switcher = new AppPageSwitcher { PrimaryPage = draft, SecondaryPage = editor };
        var layout = Assert.IsType<Grid>(switcher.Content);
        var primary = Assert.IsType<ContentPresenter>(layout.Children[0]);
        var secondary = Assert.IsType<ContentPresenter>(layout.Children[1]);

        Assert.True(primary.IsVisible);
        Assert.False(secondary.IsVisible);
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(secondary));
        switcher.IsSecondaryActive = true;
        Assert.False(primary.IsVisible);
        Assert.True(secondary.IsVisible);
        switcher.IsSecondaryActive = false;
        Assert.True(primary.IsVisible);
        Assert.False(secondary.IsVisible);
        Assert.Same(draft, primary.Content);
        Assert.Same(editor, secondary.Content);
        Assert.Equal("unsent draft", draft.Text);
        Assert.Equal("unsaved settings", editor.Text);
        Assert.True(draft.IsVisible);
        Assert.True(editor.IsVisible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementDoesNotChangeTheSelectedPage(bool secondaryActive)
    {
        var switcher = new AppPageSwitcher { IsSecondaryActive = secondaryActive };
        switcher.PrimaryPage = new AppButton();
        switcher.SecondaryPage = new AppInput();
        var layout = Assert.IsType<Grid>(switcher.Content);
        Assert.Equal(!secondaryActive, layout.Children[0].IsVisible);
        Assert.Equal(secondaryActive, layout.Children[1].IsVisible);
        Assert.Same(switcher.PrimaryPage, Assert.IsType<ContentPresenter>(layout.Children[0]).Content);
        Assert.Same(switcher.SecondaryPage, Assert.IsType<ContentPresenter>(layout.Children[1]).Content);
    }

    [Fact]
    public void FocusPrefersAnAvailableFieldAndCanRestoreThePreviousButton()
    {
        var button = new AppButton();
        var hidden = new AppInput { IsVisible = false };
        var disabled = new AppInput { IsEnabled = false };
        var search = new AppInput();
        var page = new Grid { Children = { button, hidden, disabled, search } };

        Assert.Same(search, AppPageSwitcher.SelectFocusTarget(page, null));
        Assert.Same(button, AppPageSwitcher.SelectFocusTarget(page, button));
        Assert.Same(search, AppPageSwitcher.SelectFocusTarget(page, hidden));
        Assert.Same(search, AppPageSwitcher.SelectFocusTarget(page, disabled));
        Assert.Same(search, AppPageSwitcher.SelectFocusTarget(page, new AppButton()));
    }

    [Fact]
    public void FocusCannotReturnToRemovedOrDisabledDescendants()
    {
        var nestedField = new AppInput();
        var unavailable = new Grid { IsEnabled = false, Children = { nestedField } };
        var button = new AppButton();
        var removed = new AppInput();
        var page = new Grid { Children = { unavailable, button, removed } };
        page.Children.Remove(removed);

        Assert.Same(button, AppPageSwitcher.SelectFocusTarget(page, nestedField));
        Assert.Same(button, AppPageSwitcher.SelectFocusTarget(page, removed));
        button.IsEnabled = false;
        Assert.Null(AppPageSwitcher.SelectFocusTarget(page, null));
    }

    [Fact]
    public void APageThatIsItselfFocusableCanReceiveInitialFocus()
    {
        var field = new AppInput();
        Assert.Same(field, AppPageSwitcher.SelectFocusTarget(field, null));
        field.IsVisible = false;
        Assert.Null(AppPageSwitcher.SelectFocusTarget(field, field));
    }

    [Fact]
    public void SettingsUsesTheSharedPageSwitcherAndApprovalRemainsGlobal()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "MainWindow.axaml"));
        var switcher = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppPageSwitcher"
            && element.Attribute("IsSecondaryActive")?.Value == "{Binding IsSettingsOpen}");
        Assert.Equal("1", switcher.Attribute("Grid.Row")?.Value);
        Assert.Equal("{Binding IsSettingsOpen}", switcher.Attribute("IsSecondaryActive")?.Value);
        var primary = Assert.Single(switcher.Elements(), element => element.Name.LocalName == "AppPageSwitcher.PrimaryPage");
        Assert.Contains(primary.Descendants(), element => element.Name.LocalName == "WorkspaceChatView");
        Assert.DoesNotContain(switcher.Descendants(), element => element.Name.LocalName == "ToolApprovalView");
        Assert.Equal("Continue", switcher.Attribute("SecondaryTabNavigation")?.Value);
        var approval = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ToolApprovalView");
        Assert.Equal("1", approval.Parent!.Attribute("Grid.Row")?.Value);
        var visibility = Assert.Single(approval.Parent.Elements(), element => element.Name.LocalName == "AppBorder.IsVisible");
        var conditions = Assert.Single(visibility.Elements());
        Assert.Equal("MultiBinding", conditions.Name.LocalName);
        Assert.Equal("{x:Static BoolConverters.And}", conditions.Attribute("Converter")?.Value);
        Assert.Equal(new[] { "Approval.IsPending", "!#WorkspaceNavigation.IsOverlayOpen", "!#SettingsView.Navigation.IsOverlayOpen" },
            conditions.Elements().Select(binding => binding.Attribute("Path")?.Value));
        Assert.Same(switcher.Parent, approval.Parent.Parent);
        var siblings = switcher.Parent!.Elements().ToList();
        var shutdown = Assert.Single(siblings, element => element.Name.LocalName == "AppShutdownOverlay");
        Assert.True(siblings.IndexOf(switcher) < siblings.IndexOf(approval.Parent));
        Assert.True(siblings.IndexOf(approval.Parent) < siblings.IndexOf(shutdown));
        var workspacePages = Assert.Single(primary.Descendants(), element => element.Name.LocalName == "AppPageSwitcher"
            && element.Attribute("IsSecondaryActive")?.Value == "{Binding Pages.IsAutomationsOpen}");
        Assert.Contains(workspacePages.Descendants(), element => element.Name.LocalName == "AutomationsView");
        Assert.DoesNotContain(workspacePages.Descendants(), element => element.Name.LocalName == "ToolApprovalView");
        var secondary = Assert.Single(switcher.Elements(), element => element.Name.LocalName == "AppPageSwitcher.SecondaryPage");
        var settings = Assert.Single(secondary.Elements());
        Assert.Equal("SettingsOverlay", settings.Name.LocalName);
        Assert.Null(settings.Attribute("IsVisible"));
    }

    [Theory]
    [InlineData(KeyboardNavigationMode.Continue)]
    [InlineData(KeyboardNavigationMode.Cycle)]
    public void SecondaryKeyboardNavigationCanBeConfiguredWithoutChangingThePages(KeyboardNavigationMode mode)
    {
        var draft = new AppInput { Text = "retained" };
        var switcher = new AppPageSwitcher { SecondaryPage = draft, IsSecondaryActive = true };
        var layout = Assert.IsType<Grid>(switcher.Content);
        var secondary = Assert.IsType<ContentPresenter>(layout.Children[1]);
        Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(secondary));
        switcher.SecondaryTabNavigation = mode;
        Assert.Equal(mode, KeyboardNavigation.GetTabNavigation(secondary));
        Assert.Same(draft, secondary.Content);
        Assert.Equal("retained", draft.Text);
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
