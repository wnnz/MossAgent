using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AttachmentChipEncapsulationTests
{
    [Theory]
    [InlineData("Content='replacement'")]
    [InlineData("Padding='2'")]
    [InlineData("Width='500'")]
    [InlineData("Height='90'")]
    [InlineData("MinWidth='500'")]
    [InlineData("MaxWidth='500'")]
    [InlineData("MinHeight='90'")]
    [InlineData("MaxHeight='90'")]
    [InlineData("UserControl.Content='replacement'")]
    [InlineData("Control.Width='500'")]
    [InlineData("VerticalContentAlignment='Top'")]
    [InlineData("UserControl.HorizontalContentAlignment='Left'")]
    public void MarkupGuard_RejectsAppearanceAndContentOverrides(string attributes) =>
        Assert.Single(XDocument.Parse($"<AppAttachmentChip {attributes} />").XPathSelectElements(Query()));

    [Theory]
    [InlineData("<Grid />")]
    [InlineData("<UserControl.Content><Grid /></UserControl.Content>")]
    [InlineData("<ContentControl.Padding>2</ContentControl.Padding>")]
    [InlineData("<Control.MaxWidth>500</Control.MaxWidth>")]
    public void MarkupGuard_RejectsChildAndPropertyElementOverrides(string content) =>
        Assert.Single(XDocument.Parse($"<AppAttachmentChip>{content}</AppAttachmentChip>").XPathSelectElements(Query()));

    [Fact]
    public void MarkupGuard_AllowsSemanticBindingsAndSurroundingLayout() =>
        Assert.Empty(XDocument.Parse("""
            <AppAttachmentChip DisplayPath='{Binding DisplayPath}' SizeLabel='{Binding SizeLabel}'
                               RemoveCommand='{Binding RemoveCommand}' Margin='0,4'
                               IsVisible='{Binding IsVisible}' Grid.Row='1' />
            """).XPathSelectElements(Query()));

    [Fact]
    public void BothAttachmentViews_UseOneOwnedComponentInsteadOfComposingTheAppearance()
    {
        foreach (var view in new[] { "WorkspaceComposerView.axaml", "WorkspaceMessageView.axaml" })
        {
            var document = XDocument.Load(Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views", view));
            var chip = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppAttachmentChip");
            Assert.Equal("{Binding DisplayPath}", chip.Attribute("DisplayPath")?.Value);
            Assert.Equal("{Binding SizeLabel}", chip.Attribute("SizeLabel")?.Value);
            Assert.Empty(chip.Elements());
            Assert.DoesNotContain(document.Descendants(), element =>
                element.Attribute("Classes")?.Value.Split(' ').Contains("attachment-chip") == true);
            Assert.Equal(view == "WorkspaceComposerView.axaml" ? "{Binding RemoveCommand}" : null,
                chip.Attribute("RemoveCommand")?.Value);
        }
    }

    [Theory]
    [InlineData("chip.Content = new Grid();")]
    [InlineData("chip.Padding = new Avalonia.Thickness(2);")]
    [InlineData("chip.Width = 500;")]
    [InlineData("chip.MinHeight = 90;")]
    [InlineData("chip.SetValue(UserControl.ContentProperty, new Grid());")]
    [InlineData("chip.ClearValue(Control.MaxWidthProperty);")]
    public async Task BusinessCode_CannotReplaceOwnedAppearance(string statement)
    {
        var source = "using Avalonia.Controls; using MossAgent.App.Controls; namespace MossAgent.App.Views; "
            + "public class View { public void Configure() { var chip = new AppAttachmentChip(); " + statement + " } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task BusinessCode_AllowsDataCommandsAndSharedComponentImplementation()
    {
        foreach (var source in new[]
        {
            "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { public void Configure() "
                + "{ var chip = new AppAttachmentChip { DisplayPath = \"file.txt\", SizeLabel = \"28 B\", RemoveCommand = null }; } }",
            "using Avalonia; using MossAgent.App.Controls; namespace MossAgent.App.Controls; public class Factory "
                + "{ public void Configure() { var chip = new AppAttachmentChip { Padding = new Thickness(2) }; } }"
        })
            Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    private static string Query()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedAttachmentChips");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;
    }

}
