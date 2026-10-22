using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedFlyoutAppearanceTests
{
    [Theory]
    [InlineData("flyout.FlyoutPresenterTheme = null;")]
    [InlineData("flyout.FlyoutPresenterClasses.Add(\"custom\");")]
    [InlineData("flyout.FlyoutPresenterClasses.Clear();")]
    [InlineData("flyout.FlyoutPresenterClasses.Set(\"app-flyout\", false);")]
    [InlineData("flyout.FlyoutPresenterClasses[0] = \"custom\";")]
    [InlineData("var classes = flyout.FlyoutPresenterClasses; classes.Remove(\"app-flyout\");")]
    [InlineData("((System.Collections.Generic.ICollection<string>)flyout.FlyoutPresenterClasses).Add(\"custom\");")]
    [InlineData("flyout.SetValue(Flyout.FlyoutPresenterThemeProperty, null);")]
    [InlineData("flyout.Bind(Flyout.FlyoutPresenterThemeProperty, new Avalonia.Data.Binding(\"Theme\"));")]
    public async Task BusinessCode_CannotOverrideSharedFlyoutPresenter(string body)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Configure(AppFlyout flyout) { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("FlyoutPresenter", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedImplementationAndFunctionalProperties_RemainAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedFlyout : Flyout
                {
                    public void Configure()
                    {
                        FlyoutPresenterClasses.Add("app-flyout");
                        FlyoutPresenterTheme = null;
                    }
                }
            }
            namespace MossAgent.App.Views
            {
                public class View
                {
                    public void Configure(AppFlyout flyout)
                    {
                        flyout.Content = new AppText { Text = "Details" };
                        flyout.DismissOnButtonClick = true;
                        flyout.Placement = PlacementMode.Bottom;
                        var count = flyout.FlyoutPresenterClasses.Count;
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Theory]
    [InlineData("<ui:AppFlyout FlyoutPresenterTheme='{StaticResource CustomTheme}' />")]
    [InlineData("<ui:AppFlyout FlyoutPresenterClasses='custom' />")]
    [InlineData("<ui:AppFlyout Flyout.FlyoutPresenterTheme='{StaticResource CustomTheme}' />")]
    [InlineData("<ui:AppFlyout Flyout.FlyoutPresenterClasses='custom' />")]
    [InlineData("<ui:AppFlyout><ui:AppFlyout.FlyoutPresenterTheme /></ui:AppFlyout>")]
    [InlineData("<ui:AppFlyout><Flyout.FlyoutPresenterClasses /></ui:AppFlyout>")]
    public void Markup_CannotOverrideSharedFlyoutPresenter(string markup)
    {
        var document = XDocument.Parse("<Grid xmlns='https://github.com/avaloniaui' "
            + "xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

        Assert.Single(document.XPathSelectElements(PresenterQuery()));
    }

    [Fact]
    public void Markup_FunctionalFlyoutPropertiesRemainAllowed()
    {
        var document = XDocument.Parse("""
            <ui:AppFlyout xmlns="https://github.com/avaloniaui"
                          xmlns:ui="clr-namespace:MossAgent.App.Controls"
                          Placement="Bottom" DismissOnButtonClick="True">
              <ui:AppCard Classes="card-inset-roomy"><ui:AppText Text="Details" /></ui:AppCard>
            </ui:AppFlyout>
            """);

        Assert.Empty(document.XPathSelectElements(PresenterQuery()));
    }

    private static string PresenterQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedVisualTemplates");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }
}
