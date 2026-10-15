using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedVisualControlAnalyzerTests
{
    [Fact]
    public async Task NativeControlsAndFlyouts_AreRejectedOutsideWrappers()
    {
        var source = """
            using Avalonia.Controls;
            using Avalonia.Controls.Primitives;
            using NativeButton = Avalonia.Controls.Button;
            namespace MossAgent.App.Views
            {
                public class BadView : Button
                {
                    public void Render()
                    {
                        _ = new Button();
                        Button second = new();
                        _ = new NativeButton();
                        _ = new Flyout();
                    }
                }
            }
            """;

        var diagnostics = await Analyze(source);
        Assert.Equal(5, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal(SharedVisualControlAnalyzer.DiagnosticId, diagnostic.Id));
    }

    [Fact]
    public async Task SharedWrappersAndStructuralLayout_AreAllowed()
    {
        var source = """
            using Avalonia.Controls;
            namespace MossAgent.App.Controls
            {
                public class LocalWrapper : Button
                {
                    public LocalWrapper() { _ = new Button(); }
                }
            }
            namespace MossAgent.App.Views
            {
                public class GoodView : MossAgent.App.Controls.AppWindow
                {
                    public void Render()
                    {
                        _ = new MossAgent.App.Controls.AppButton();
                        _ = new Grid();
                    }
                }
            }
            """;

        Assert.Empty(await Analyze(source));
    }

    [Fact]
    public async Task ThirdPartyVisuals_AlsoRequireWrappers()
    {
        var source = """
            namespace Vendor
            {
                public class Widget : Avalonia.Controls.Control { }
            }
            namespace MossAgent.App.Views
            {
                public class Host
                {
                    public void Render() { _ = new Vendor.Widget(); }
                }
            }
            """;

        Assert.Equal(2, (await Analyze(source)).Length);
    }

    [Fact]
    public void AppBuild_LoadsTheVisualControlAnalyzer()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var reference = Assert.Single(project.Descendants("ProjectReference"), element =>
            element.Attribute("Include")?.Value.Contains("MossAgent.App.Analyzers", StringComparison.Ordinal) == true);
        Assert.Equal("Analyzer", reference.Attribute("OutputItemType")?.Value);
        Assert.Equal("false", reference.Attribute("ReferenceOutputAssembly")?.Value);
    }

    [Theory]
    [InlineData("public class BadView() : Avalonia.Controls.Button();")]
    [InlineData("public class Factory { public T Create<T>() where T : Avalonia.Controls.Control, new() => new T(); }")]
    [InlineData("public class Factory { public T Create<T>() where T : Avalonia.Controls.Button, new() => new(); }")]
    public async Task AlternateNativeConstructionSyntax_IsRejected(string body)
    {
        var diagnostic = Assert.Single(await Analyze("namespace MossAgent.App.Views { " + body + " }"));

        Assert.Equal(SharedVisualControlAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task GenericSharedComponentsAndPrimaryConstructorHosts_AreAllowed()
    {
        var source = """
            namespace MossAgent.App.Views
            {
                public class View() : MossAgent.App.Controls.AppUserControl();
                public class Factory
                {
                    public T Create<T>() where T : MossAgent.App.Controls.AppButton, new() => new T();
                    public T Layout<T>() where T : Avalonia.Controls.Panel, new() => new();
                }
            }
            """;

        Assert.Empty(await Analyze(source));
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(string source) =>
        VisualPolicyTestCompilation.Analyze(source, new SharedVisualControlAnalyzer());

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
