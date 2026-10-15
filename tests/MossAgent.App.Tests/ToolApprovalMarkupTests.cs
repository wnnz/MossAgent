using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ToolApprovalMarkupTests
{
    [Fact]
    public void ContentVisibility_UsesThePreviewDataContext()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var view = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "Views", "ToolApprovalView.axaml"));
        var preview = Assert.Single(view.Descendants(), element => element.Attribute("DataContext")?.Value
            == "{Binding ContentPreview}");

        Assert.Equal("{Binding IsAvailable}", preview.Attribute("IsVisible")?.Value);
        Assert.Contains(preview.Descendants(), element => element.Name.LocalName == "AppSelectableText"
            && element.Attribute("Text")?.Value == "{Binding PageText}");
        Assert.Contains(preview.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Command")?.Value == "{Binding PreviousCommand}");
        Assert.Contains(preview.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Command")?.Value == "{Binding NextCommand}");
    }
}
