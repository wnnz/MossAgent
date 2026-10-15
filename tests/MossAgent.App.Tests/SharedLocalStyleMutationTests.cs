using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedLocalStyleMutationTests
{
    [Theory]
    [InlineData("button.Styles.Add(style);")]
    [InlineData("button.Styles.AddRange(new[] { style });")]
    [InlineData("button.Styles.Insert(0, style);")]
    [InlineData("button.Styles.Remove(style);")]
    [InlineData("button.Styles.RemoveAt(0);")]
    [InlineData("button.Styles.Clear();")]
    [InlineData("button.Styles[0] = style;")]
    [InlineData("((System.Collections.Generic.ICollection<Avalonia.Styling.IStyle>)button.Styles).Add(style);")]
    [InlineData("Styles.Add(style);")]
    public async Task ViewCode_CannotMutateLocalStyleCollections(string body)
    {
        var source = $$"""
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View : AppUserControl
            {
                public void Render(AppButton button, Avalonia.Styling.IStyle style) { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Styles", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SharedComponentsAndApplicationStyles_RemainAllowed()
    {
        var source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedButton : AppButton
                {
                    public void Configure(Avalonia.Styling.IStyle style) { Styles.Add(style); }
                }
            }
            namespace MossAgent.App
            {
                public class App : Avalonia.Application
                {
                    public void Configure(Avalonia.Styling.IStyle style) { Styles.Add(style); }
                }
            }
            namespace MossAgent.App.Views
            {
                public class View
                {
                    public void Render(AppButton button)
                    {
                        button.Classes.Add("primary");
                        button.Classes.Remove("danger");
                        var count = button.Styles.Count;
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }
}
