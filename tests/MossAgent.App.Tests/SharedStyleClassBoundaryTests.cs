using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedStyleClassBoundaryTests
{
    [Theory]
    [InlineData("component.Classes.Clear();")]
    [InlineData("component.Classes.Remove(\"app-input\");")]
    [InlineData("component.Classes.Set(\"app-input\", false);")]
    [InlineData("component.Classes.Add(\"app-combo-box\");")]
    [InlineData("_ = new AppInput { Classes = { \"app-input\" } };")]
    [InlineData("component.Classes.Insert(0, \"app-input\");")]
    [InlineData("component.Classes.RemoveAt(0);")]
    [InlineData("component.Classes.RemoveRange(0, 1);")]
    [InlineData("component.Classes.RemoveAll(new[] { \"app-input\" });")]
    [InlineData("component.Classes.AddRange(new[] { \"app-input\" });")]
    [InlineData("component.Classes.InsertRange(0, new[] { \"app-input\" });")]
    [InlineData("component.Classes.Move(0, 1);")]
    [InlineData("component.Classes.MoveRange(0, 1, 0);")]
    [InlineData("component.Classes[0] = \"selected\";")]
    [InlineData("((System.Collections.Generic.IList<string>)component.Classes)[0] = \"selected\";")]
    [InlineData("((System.Collections.Generic.ICollection<string>)component.Classes).Clear();")]
    [InlineData("var classes = (component.Classes); classes!.Clear();")]
    [InlineData("System.Collections.Generic.IList<string> classes = component.Classes; classes.RemoveAt(0);")]
    [InlineData("const string prefix = \"app-\"; component.Classes.Remove(prefix + \"input\");")]
    [InlineData("var name = System.DateTime.Now.ToString(); component.Classes.Remove(name);")]
    [InlineData("var name = System.DateTime.Now.ToString(); component.Classes.Set(name, false);")]
    public async Task BusinessCode_CannotRemoveOrImpersonateSharedStyleIdentity(string body)
    {
        var source = Source("MossAgent.App.Views", body);
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));

        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Classes", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamedVariantsFunctionalStateAndClassInspection_RemainAllowed()
    {
        var source = Source("MossAgent.App.Views", """
            component.Classes.Add("field-multiline");
            component.Classes.Remove("field-multiline");
            component.Classes.Set("selected", component.IsEnabled);
            var variant = "compact-composer";
            component.Classes.Remove(variant);
            System.Collections.Generic.ICollection<string> classes = component.Classes;
            classes.Add("field-prompt");
            _ = classes.Contains("app-input");
            _ = component.Classes[0];
            _ = component.Classes.Count;
            component.Text = "draft";
            component.IsReadOnly = true;
            """);

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task SharedComponentImplementation_CanOwnItsStyleIdentity()
    {
        var source = Source("MossAgent.App.Controls", """
            component.Classes.Clear();
            component.Classes.Add("app-input");
            component.Classes.Set("app-input", true);
            """);

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task GenericControls_CannotClearSharedStyleIdentity()
    {
        var source = """
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render<T>(T component) where T : Avalonia.Controls.Control
                {
                    component.Classes.Clear();
                }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task UnrelatedCollections_AreNotSharedStyleIdentity()
    {
        var source = Source("MossAgent.App.Views", """
            var classes = new System.Collections.Generic.List<string> { "app-input" };
            classes.Clear();
            classes.Add("app-input");
            classes.RemoveAt(0);
            """);

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    private static string Source(string owner, string body) => $$"""
        using MossAgent.App.Controls;
        namespace {{owner}};
        public class View
        {
            public void Render()
            {
                var component = new AppInput();
                {{body}}
            }
        }
        """;
}
