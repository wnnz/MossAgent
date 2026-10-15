using Avalonia.Controls;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class VisualControlInheritanceTests
{
    [Fact]
    public void ReusableVisualControls_LiveInSharedComponentNamespace()
    {
        var visualControls = typeof(MainWindow).Assembly.GetTypes()
            .Where(type => type.IsClass
                && !type.IsAbstract
                && typeof(Control).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(visualControls);
        Assert.All(visualControls, type =>
        {
            if (type.Namespace == typeof(AppSelectableText).Namespace) return;

            Assert.Equal(typeof(MainWindow).Namespace, type.Namespace);
            Assert.True(type.BaseType == typeof(AppUserControl) || type.BaseType == typeof(AppWindow),
                $"{type.FullName} is a reusable control outside MossAgent.App.Controls");
        });
    }

    [Fact]
    public void RichTextControls_UseSharedSelectableTextBase()
    {
        Assert.Equal(typeof(AppSelectableText), typeof(MarkdownSelectableTextBlock).BaseType);
        Assert.Equal(typeof(AppSelectableText), typeof(SyntaxHighlightedSelectableTextBlock).BaseType);
    }
}
