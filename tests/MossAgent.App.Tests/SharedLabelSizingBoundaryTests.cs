using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedLabelSizingBoundaryTests
{
    [Theory]
    [InlineData("label.LabelMaxWidth = 90;")]
    [InlineData("action.LabelMaxWidth = 90;")]
    [InlineData("label.SetValue(AppIconLabel.LabelMaxWidthProperty, 90d);")]
    [InlineData("action.Bind(AppActionButton.LabelMaxWidthProperty, new Avalonia.Data.Binding(\"Width\"));")]
    [InlineData("var width = AppIconLabel.LabelMaxWidthProperty; label.SetCurrentValue(width, 90d);")]
    [InlineData("((AppIconLabel)target).LabelMaxWidth = 90;")]
    [InlineData("label[AppIconLabel.LabelMaxWidthProperty] = 90d;")]
    [InlineData("label[!AppIconLabel.LabelMaxWidthProperty] = new Avalonia.Data.Binding(\"Width\");")]
    public async Task ViewCode_CannotOverrideSharedLabelSizing(string body)
    {
        var source = $$"""
            using Avalonia;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Configure(AppIconLabel label, AppActionButton action, AvaloniaObject target)
                {
                    {{body}}
                }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("LabelMaxWidth", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<ui:AppIconLabel LabelMaxWidth='90' />")]
    [InlineData("<ui:AppActionButton LabelMaxWidth='90' />")]
    [InlineData("<ui:AppIconLabel ui:AppIconLabel.LabelMaxWidth='90' />")]
    [InlineData("<ui:AppActionButton ui:AppActionButton.LabelMaxWidth='90' />")]
    [InlineData("<ui:AppIconLabel><ui:AppIconLabel.LabelMaxWidth>90</ui:AppIconLabel.LabelMaxWidth></ui:AppIconLabel>")]
    [InlineData("<ui:AppActionButton><ui:AppActionButton.LabelMaxWidth>90</ui:AppActionButton.LabelMaxWidth></ui:AppActionButton>")]
    [InlineData("<ui:AppIconLabel><ui:AppIconLabel.LabelMaxWidth><Binding Path='Width' /></ui:AppIconLabel.LabelMaxWidth></ui:AppIconLabel>")]
    public void Markup_CannotOverrideSharedLabelSizing(string body)
    {
        var document = XDocument.Parse($$"""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              {{body}}
            </ui:AppUserControl>
            """);

        Assert.Single(document.XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public async Task SharedImplementationAndFunctionalProperties_RemainAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedLayout
                {
                    public void Configure(AppIconLabel label, AppActionButton action)
                    {
                        label.LabelMaxWidth = 200;
                        action.SetValue(AppActionButton.LabelMaxWidthProperty, 160d);
                    }
                }
            }
            namespace MossAgent.App.Views
            {
                public class View
                {
                    public void Configure(AppIconLabel label, AppActionButton action, Grid layout)
                    {
                        label.Label = "Branch";
                        label.IconData = null;
                        label.Classes.Add("composer-context-label");
                        action.Label = "Project";
                        action.IsEnabled = true;
                        action.CommandParameter = "project-id";
                        layout.MaxWidth = 736;
                        var width = label.LabelMaxWidth;
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public void SharedVariants_PreserveExistingLabelLimits()
    {
        var styles = XDocument.Load(Path.Combine(AppPath(), "Styles", "SurfaceVariants.axaml"));
        foreach (var variant in new[] { "composer-context-label", "project-summary-title", "project-summary-path" })
        {
            var style = Assert.Single(styles.Descendants(), element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value.Split(", ")
                    .Contains("controls|AppIconLabel." + variant) == true);
            var setter = Assert.Single(style.Elements(), element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "LabelMaxWidth");
            Assert.Equal(variant == "composer-context-label" ? "200" : "260", setter.Attribute("Value")?.Value);
        }
    }

    [Fact]
    public void AllBusinessMarkup_UsesSharedLabelSizing()
    {
        var query = GuardQuery();
        foreach (var file in Directory.EnumerateFiles(AppPath(), "*.axaml", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(AppPath(), file);
            if (relativePath == "App.axaml" || relativePath.Split(Path.DirectorySeparatorChar)[0]
                is "Styles" or "bin" or "obj") continue;

            Assert.Empty(XDocument.Load(file).XPathSelectElements(query));
        }
    }

    private static string GuardQuery()
    {
        var project = XDocument.Load(Path.Combine(AppPath(), "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedLabelSizing");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        return Assert.IsType<string>(Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value);
    }

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
