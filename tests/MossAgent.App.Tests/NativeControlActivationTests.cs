using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NativeControlActivationTests
{
    [Theory]
    [InlineData("_ = System.Activator.CreateInstance<Avalonia.Controls.Button>();")]
    [InlineData("_ = System.Activator.CreateInstance(typeof(Avalonia.Controls.TextBox));")]
    [InlineData("var type = typeof(Avalonia.Controls.Flyout); _ = System.Activator.CreateInstance(type);")]
    [InlineData("_ = System.Activator.CreateInstance(typeof(Avalonia.Controls.Button), nonPublic: true);")]
    public async Task ReflectiveNativeVisualConstructionIsRejected(string body)
    {
        var source = "namespace MossAgent.App.Views { public class Factory { public void Render() { " + body + " } } }";

        var diagnostics = await VisualPolicyTestCompilation.Analyze(source, new SharedVisualControlAnalyzer());

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SharedVisualControlAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("_ = System.Activator.CreateInstance<MossAgent.App.Controls.AppButton>();")]
    [InlineData("_ = System.Activator.CreateInstance(typeof(MossAgent.App.Controls.AppFlyout));")]
    [InlineData("_ = System.Activator.CreateInstance(typeof(Avalonia.Controls.Grid));")]
    [InlineData("_ = System.Activator.CreateInstance(typeof(System.Collections.Generic.List<string>));")]
    public async Task SharedControlsLayoutAndNonVisualConstructionRemainAllowed(string body)
    {
        var source = "namespace MossAgent.App.Views { public class Factory { public void Render() { " + body + " } } }";

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedVisualControlAnalyzer()));
    }
}
