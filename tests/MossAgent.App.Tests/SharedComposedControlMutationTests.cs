using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedComposedControlMutationTests
{
    [Theory]
    [InlineData("label.Children.Add(new AppText());", "Children")]
    [InlineData("label.Children.Clear();", "Children")]
    [InlineData("label.Children.RemoveAt(0);", "Children")]
    [InlineData("label.Children[0] = new AppText();", "Children")]
    [InlineData("((System.Collections.Generic.ICollection<Control>)label.Children).Clear();", "Children")]
    [InlineData("var children = label.Children; children.Clear();", "Children")]
    [InlineData("var layout = (Grid)label; layout.Children.Clear();", "Children")]
    [InlineData("label.ColumnDefinitions.Clear();", "ColumnDefinitions")]
    [InlineData("label.ColumnDefinitions.Add(new ColumnDefinition());", "ColumnDefinitions")]
    [InlineData("label.ColumnDefinitions[0] = new ColumnDefinition();", "ColumnDefinitions")]
    [InlineData("var columns = label.ColumnDefinitions; columns.RemoveAt(0);", "ColumnDefinitions")]
    [InlineData("label.RowDefinitions.Clear();", "RowDefinitions")]
    [InlineData("label.RowDefinitions.Add(new RowDefinition());", "RowDefinitions")]
    [InlineData("label.RowDefinitions[0] = new RowDefinition();", "RowDefinitions")]
    public async Task Pages_CannotMutateComposedControlStructure(string body, string property)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render(AppIconLabel label) { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains(property, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ui:AppIconLabel.Children")]
    [InlineData("Panel.Children")]
    [InlineData("Grid.ColumnDefinitions")]
    [InlineData("Grid.RowDefinitions")]
    public void Markup_CannotReplaceComposedControlCollections(string property)
    {
        var document = XDocument.Parse($$"""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppIconLabel>
                <{{property}} />
              </ui:AppIconLabel>
            </ui:AppUserControl>
            """);

        Assert.Equal("AppIconLabel", Assert.Single(document.XPathSelectElements(GuardQuery())).Name.LocalName);
    }

    [Fact]
    public async Task SharedImplementationAndPageLayout_RemainAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedLayout
                {
                    public void Configure(AppIconLabel label)
                    {
                        label.Children.Clear();
                        label.ColumnDefinitions.Clear();
                    }
                }
            }
            namespace MossAgent.App.Views
            {
                public class View
                {
                    public void Configure(AppIconLabel label, Grid layout)
                    {
                        label.Label = "Title";
                        label.IsVisible = true;
                        label.Classes.Add("muted");
                        var count = label.Children.Count;
                        layout.Children.Add(label);
                        layout.ColumnDefinitions.Add(new ColumnDefinition());
                        layout.RowDefinitions.Clear();
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public void MarkupBehaviorAndStructuralLayout_RemainAllowed()
    {
        var document = XDocument.Parse("""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <Grid>
                <Grid.ColumnDefinitions><ColumnDefinition /></Grid.ColumnDefinitions>
                <Grid.Children>
                  <ui:AppIconLabel Classes="muted" Margin="4">
                    <ui:AppIconLabel.Label><Binding Path="Title" /></ui:AppIconLabel.Label>
                  </ui:AppIconLabel>
                </Grid.Children>
              </Grid>
            </ui:AppUserControl>
            """);

        Assert.Empty(document.XPathSelectElements(GuardQuery()));
    }

    private static string GuardQuery()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedComposedControls");
        return Assert.IsType<string>(Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value);
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
