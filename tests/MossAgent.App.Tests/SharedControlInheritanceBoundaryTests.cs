using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedControlInheritanceBoundaryTests
{
    [Theory]
    [InlineData("MossAgent.App.Controls.AppButton")]
    [InlineData("MossAgent.App.Controls.AppSelectableText")]
    public async Task BusinessCode_CannotDeclareItsOwnVisualComponent(string baseType)
    {
        var source = $$"""
            namespace MossAgent.App.Views;
            public class LocalComponent : {{baseType}} { }
            """;

        var diagnostic = Assert.Single(await Analyze(source));

        Assert.Equal(SharedVisualControlAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("MossAgent.App.Controls.AppButton")]
    [InlineData("MossAgent.App.Controls.AppSelectableText")]
    public async Task PrimaryConstructor_CannotBypassComponentOwnership(string baseType)
    {
        var source = $$"""
            namespace MossAgent.App.Views;
            public class LocalComponent() : {{baseType}}();
            """;

        var diagnostic = Assert.Single(await Analyze(source));

        Assert.Equal(SharedVisualControlAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("MossAgent.App.Controls.AppUserControl")]
    [InlineData("MossAgent.App.Controls.AppWindow")]
    public async Task ViewHosts_MustBelongToViews(string baseType)
    {
        var source = $$"""
            namespace MossAgent.App.Features;
            public class LocalView : {{baseType}} { }
            """;

        Assert.Single(await Analyze(source));
    }

    [Fact]
    public async Task BusinessViews_CannotBecomeReusableBaseComponents()
    {
        var source = """
            namespace MossAgent.App.Views;
            public class Page : MossAgent.App.Controls.AppUserControl { }
            public class DerivedPage : Page { }
            """;

        Assert.Single(await Analyze(source));
    }

    [Fact]
    public async Task SharedImplementationsAndDirectViewHosts_RemainAllowed()
    {
        var source = """
            namespace MossAgent.App.Controls
            {
                public class SharedComponent : AppButton { }
                public class SharedDerivedComponent : SharedComponent { }
            }
            namespace MossAgent.App.Views
            {
                public class Page : MossAgent.App.Controls.AppUserControl { }
                public class PrimaryPage() : MossAgent.App.Controls.AppUserControl();
                public class MainWindow : MossAgent.App.Controls.AppWindow { }
                public class PrimaryWindow() : MossAgent.App.Controls.AppWindow();
                public class StructuralLayout : Avalonia.Controls.Grid { }
                public class Factory
                {
                    public void Render()
                    {
                        _ = new MossAgent.App.Controls.SharedDerivedComponent();
                        _ = new Page();
                        _ = new MainWindow();
                    }
                }
            }
            """;

        Assert.Empty(await Analyze(source));
    }

    private static Task<ImmutableArray<Diagnostic>> Analyze(
        string source) => VisualPolicyTestCompilation.Analyze(source, new SharedVisualControlAnalyzer());
}
