using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NativeGenericConstructionTests
{
    [Theory]
    [InlineData("_ = Factory.Create<Avalonia.Controls.Button>();")]
    [InlineData("_ = Factory.Create<Avalonia.Controls.TextBox>();")]
    [InlineData("_ = Factory.Create<Avalonia.Controls.Flyout>();")]
    [InlineData("System.Func<Avalonia.Controls.Button> create = Factory.Create<Avalonia.Controls.Button>;")]
    [InlineData("_ = new Factory<Avalonia.Controls.Button>();")]
    [InlineData("_ = Factory<Avalonia.Controls.Button>.Create();")]
    [InlineData("System.Func<Avalonia.Controls.Button> create = Factory<Avalonia.Controls.Button>.Create;")]
    [InlineData("_ = Factory.Create<NativeButton>();")]
    public async Task NativeVisuals_CannotBeConstructedThroughGenericFactories(string body)
    {
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(
            Source(body), new SharedVisualControlAnalyzer()));

        Assert.Equal(SharedVisualControlAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains(body.Contains("TextBox", StringComparison.Ordinal) ? "TextBox"
            : body.Contains("Flyout", StringComparison.Ordinal) ? "Flyout" : "Button", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("_ = Factory.Create<MossAgent.App.Controls.AppButton>();")]
    [InlineData("_ = Factory.Create<MossAgent.App.Controls.AppFlyout>();")]
    [InlineData("_ = Factory.Create<Avalonia.Controls.Grid>();")]
    [InlineData("_ = Factory.Create<System.Text.StringBuilder>();")]
    [InlineData("System.Func<MossAgent.App.Controls.AppButton> create = Factory.Create<MossAgent.App.Controls.AppButton>;")]
    [InlineData("_ = new Factory<MossAgent.App.Controls.AppButton>();")]
    [InlineData("_ = Factory<MossAgent.App.Controls.AppButton>.Create();")]
    [InlineData("System.Func<Avalonia.Controls.Grid> create = Factory<Avalonia.Controls.Grid>.Create;")]
    [InlineData("_ = new System.Collections.Generic.List<Avalonia.Controls.Button>();")]
    [InlineData("_ = Factory.Preserve<Avalonia.Controls.Button>(null);")]
    public async Task SharedComponentsLayoutAndNonConstructingGenerics_RemainAllowed(string body)
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(Source(body), new SharedVisualControlAnalyzer()));
    }

    [Fact]
    public async Task SharedImplementation_CanUseGenericNativeFactories()
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(
            Source("_ = Factory.Create<Avalonia.Controls.Button>();", "MossAgent.App.Controls"),
            new SharedVisualControlAnalyzer()));
    }

    private static string Source(string body, string owner = "MossAgent.App.Views") => $$"""
        using NativeButton = Avalonia.Controls.Button;
        namespace {{owner}};
        public class Host
        {
            public void Render() { {{body}} }
        }
        public static class Factory
        {
            public static T Create<T>() where T : new() => new T();
            public static T Preserve<T>(T value) => value;
        }
        public class Factory<T> where T : new()
        {
            public static T Create() => new T();
        }
        """;
}
