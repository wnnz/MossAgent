using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedVisualPolicyEnforcementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComponentRules_AreMandatoryErrors(bool appearance)
    {
        var rule = Assert.Single(Analyzer(appearance).SupportedDiagnostics);

        Assert.Equal(DiagnosticSeverity.Error, rule.DefaultSeverity);
        Assert.True(rule.IsEnabledByDefault);
        Assert.Contains(WellKnownDiagnosticTags.NotConfigurable, rule.CustomTags);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComponentRules_CannotBeDisabledByPragma(bool appearance)
    {
        var analyzer = Analyzer(appearance);
        var rule = Assert.Single(analyzer.SupportedDiagnostics);
        var source = $"#pragma warning disable {rule.Id}\n" + Source(appearance);

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, analyzer));

        Assert.Equal(rule.Id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(diagnostic.IsSuppressed);
    }

    [Theory]
    [InlineData(false, ReportDiagnostic.Suppress)]
    [InlineData(false, ReportDiagnostic.Warn)]
    [InlineData(false, ReportDiagnostic.Info)]
    [InlineData(false, ReportDiagnostic.Hidden)]
    [InlineData(true, ReportDiagnostic.Suppress)]
    [InlineData(true, ReportDiagnostic.Warn)]
    [InlineData(true, ReportDiagnostic.Info)]
    [InlineData(true, ReportDiagnostic.Hidden)]
    public async Task ComponentRules_CannotBeDisabledOrDowngraded(bool appearance, ReportDiagnostic level)
    {
        var analyzer = Analyzer(appearance);
        var rule = Assert.Single(analyzer.SupportedDiagnostics);
        var options = ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(rule.Id, level);

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(Source(appearance), analyzer, options));

        Assert.Equal(rule.Id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(diagnostic.IsSuppressed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StructuralLayoutAndSharedVariants_RemainAllowed(bool appearance)
    {
        var source = """
            using Avalonia.Controls;
            using Avalonia.Controls.Presenters;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View : AppUserControl
            {
                public void Render()
                {
                    _ = new Grid { ColumnSpacing = 8 };
                    _ = new StackPanel { Spacing = 8 };
                    _ = new WrapPanel();
                    _ = new Panel();
                    var button = new AppIconButton { Label = "Close" };
                    button.Classes.Add("icon-compact");
                    button.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("CanClose"));
                    _ = new ContentPresenter { Content = button };
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, Analyzer(appearance)));
    }

    private static DiagnosticAnalyzer Analyzer(bool appearance) => appearance
        ? new SharedControlAppearanceAnalyzer() : new SharedVisualControlAnalyzer();

    private static string Source(bool appearance)
    {
        var body = appearance
            ? "_ = new AppButton { FontSize = 28 };"
            : "_ = new Avalonia.Controls.Button();";
        return $$"""
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render() { {{body}} }
            }
            """;
    }
}
