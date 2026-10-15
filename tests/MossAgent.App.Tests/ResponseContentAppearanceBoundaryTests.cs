using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ResponseContentAppearanceBoundaryTests
{
    [Theory]
    [InlineData("CodeFontFamily", "new Avalonia.Media.FontFamily(\"Consolas\")")]
    [InlineData("CodeFontSize", "20d")]
    [InlineData("CodeBackground", "Avalonia.Media.Brushes.Red")]
    [InlineData("CodeBackgroundPadding", "new Avalonia.Thickness(8)")]
    [InlineData("CodeCornerRadius", "10d")]
    [InlineData("SelectionBrush", "Avalonia.Media.Brushes.Red")]
    [InlineData("SelectionForegroundBrush", "Avalonia.Media.Brushes.Red")]
    public async Task BusinessCodeCannotOverrideInlineCodeAppearance(string property, string value)
    {
        foreach (var body in new[]
        {
            $"var text = new MarkdownSelectableTextBlock {{ {property} = {value} }};",
            $"var text = new MarkdownSelectableTextBlock(); text.SetValue(MarkdownSelectableTextBlock.{property}Property, {value});",
            $"var text = new MarkdownSelectableTextBlock(); text.Bind(MarkdownSelectableTextBlock.{property}Property, new Avalonia.Data.Binding(\"Value\"));",
            $"var text = new MarkdownSelectableTextBlock(); text.ClearValue(MarkdownSelectableTextBlock.{property}Property);"
        })
        {
            var source = $$"""
                using Avalonia;
                using MossAgent.App.Controls;
                namespace MossAgent.App.Views;
                public class View { public void Configure() { {{body}} } }
                """;
            var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
                new SharedControlAppearanceAnalyzer()));
            Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
            Assert.Contains($"'{property}'", diagnostic.GetMessage(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("CodeFontFamily", "Consolas")]
    [InlineData("CodeFontSize", "20")]
    [InlineData("CodeBackground", "Red")]
    [InlineData("CodeBackgroundPadding", "8")]
    [InlineData("CodeCornerRadius", "10")]
    [InlineData("SelectionBrush", "Red")]
    [InlineData("SelectionForegroundBrush", "Red")]
    public void BusinessMarkupCannotOverrideInlineCodeAppearance(string property, string value)
    {
        foreach (var markup in new[]
        {
            $"<ui:MarkdownSelectableTextBlock {property}='{value}' />",
            $"<ui:MarkdownSelectableTextBlock ui:MarkdownSelectableTextBlock.{property}='{value}' />",
            $"<ui:MarkdownSelectableTextBlock><ui:MarkdownSelectableTextBlock.{property}>{value}</ui:MarkdownSelectableTextBlock.{property}></ui:MarkdownSelectableTextBlock>",
            $"<ui:AppSelectableText ui:MarkdownSelectableTextBlock.{property}='{value}' />"
        })
        {
            Assert.Single(Document(markup).XPathSelectElements(Query()));
        }
    }

    [Fact]
    public async Task SharedImplementationsAndFunctionalTextPropertiesRemainAllowed()
    {
        var source = """
            using Avalonia;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedComponent
                {
                    public void Configure(MarkdownSelectableTextBlock text)
                    {
                        text.CodeFontSize = 12;
                        text.CodeBackground = Avalonia.Media.Brushes.Gray;
                        text.CodeFontFamily = new Avalonia.Media.FontFamily("Consolas");
                    }
                }
            }
            namespace MossAgent.App.Views
            {
                public class View
                {
                    public void Configure(MarkdownSelectableTextBlock text)
                    {
                        text.Markdown = "`path`";
                        text.SelectionStart = 0;
                        text.SelectionEnd = 4;
                        text.ClearValue(MarkdownSelectableTextBlock.MarkdownProperty);
                    }
                }
            }
            """;
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Empty(Document("<ui:MarkdownSelectableTextBlock Markdown='`path`' SelectionStart='0' SelectionEnd='4' />")
            .XPathSelectElements(Query()));
    }

    private static XDocument Document(string markup) => XDocument.Parse($$"""
        <ui:AppUserControl xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
          {{markup}}
        </ui:AppUserControl>
        """);

    private static string Query()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), target => target.Attribute("Name")?.Value == "ValidateSharedAppearanceTokens");
        return Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;
    }
}
