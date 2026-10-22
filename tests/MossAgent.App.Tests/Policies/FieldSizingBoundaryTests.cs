using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FieldSizingBoundaryTests
{
    [Theory]
    [InlineData("new AppInput { Height = 40 }")]
    [InlineData("new AppInput { MinHeight = 40 }")]
    [InlineData("new AppInput { MaxHeight = 40 }")]
    [InlineData("new AppInput { VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top }")]
    [InlineData("new AppInput { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center }")]
    [InlineData("new AppComboBox { Height = 40 }")]
    [InlineData("new AppNumericUpDown { MinHeight = 40 }")]
    [InlineData("new AppComboBox().SetValue(Avalonia.Controls.Control.HeightProperty, 40)")]
    [InlineData("new AppNumericUpDown().ClearValue(Avalonia.Controls.Control.MinHeightProperty)")]
    [InlineData("((Avalonia.Controls.TextBox)new AppInput()).SetCurrentValue(Avalonia.Controls.Control.MaxHeightProperty, 40)")]
    public async Task BusinessCode_CannotOverrideFieldMetrics(string expression)
    {
        var source = "using Avalonia; using MossAgent.App.Controls; "
            + "namespace MossAgent.App.Views { public class View { public void Render() { "
            + expression + "; } } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));

        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppInput Height='40' />")]
    [InlineData("<ui:AppInput Control.MinHeight='40' />")]
    [InlineData("<ui:AppComboBox MaxHeight='40' />")]
    [InlineData("<ui:AppNumericUpDown VerticalContentAlignment='Top' />")]
    [InlineData("<ui:AppInput><ui:AppInput.Height>40</ui:AppInput.Height></ui:AppInput>")]
    [InlineData("<ui:AppInput><TextBox.MinHeight>40</TextBox.MinHeight></ui:AppInput>")]
    [InlineData("<ui:AppComboBox><Control.MaxHeight>40</Control.MaxHeight></ui:AppComboBox>")]
    [InlineData("<ui:AppInput><TextBox.VerticalContentAlignment>Top</TextBox.VerticalContentAlignment></ui:AppInput>")]
    public void BusinessMarkup_CannotOverrideFieldMetrics(string markup)
    {
        Assert.Single(Document(markup).XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public async Task FunctionalDataSharedVariantsAndStructuralWidths_RemainAllowed()
    {
        var source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views
            {
                public class View
                {
                    public void Render()
                    {
                        var input = new AppInput { Text = "draft", AcceptsReturn = true, Width = 240 };
                        input.Classes.Add("field-multiline");
                        input.IsReadOnly = true;
                        _ = new AppComboBox { SelectedIndex = 0, MinWidth = 120 };
                        _ = new AppNumericUpDown { Value = 42, Minimum = 0, Maximum = 100 };
                        _ = new Avalonia.Controls.Grid { Height = 120 };
                    }
                }
            }
            """;
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Empty(Document("""
            <Grid Height="120">
              <ui:AppInput Text="{Binding Draft}" Classes="field-multiline" AcceptsReturn="True" Width="240" />
              <ui:AppComboBox ItemsSource="{Binding Options}" SelectedItem="{Binding Selected}" MinWidth="120" />
              <ui:AppNumericUpDown Value="{Binding Count}" Minimum="0" Maximum="100" />
            </Grid>
            """).XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public void AllBusinessMarkup_UsesSharedFieldMetrics()
    {
        var appPath = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App");
        foreach (var file in Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(appPath, file);
            if (relative == "App.axaml" || relative.Split(Path.DirectorySeparatorChar)[0] is "Styles" or "bin" or "obj")
                continue;
            Assert.True(!XDocument.Load(file).XPathSelectElements(GuardQuery()).Any(),
                $"{relative} overrides shared field metrics.");
        }
    }

    private static XDocument Document(string content) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>"
        + content + "</Grid>");

    private static string GuardQuery()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"),
            element => element.Attribute("Name")?.Value == "ValidateSharedFieldSizing");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
