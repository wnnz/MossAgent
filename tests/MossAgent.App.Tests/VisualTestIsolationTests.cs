using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class VisualTestIsolationTests
{
    [Fact]
    public void DirectVisualConstruction_UsesTheSerializedVisualCollection()
    {
        var visualNames = new[] { typeof(Control).Assembly, typeof(AppButton).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(Control).IsAssignableFrom(type) || typeof(FlyoutBase).IsAssignableFrom(type))
            .Select(type => type.Name).Append(nameof(App)).ToHashSet(StringComparer.Ordinal);
        var failures = new List<string>();
        foreach (var path in Directory.EnumerateFiles(TestSourceDirectory(), "*.cs"))
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(path), cancellationToken: cancellationToken)
                .GetRoot(cancellationToken);
            foreach (var declaration in syntax.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsTestClass(declaration) || IsSerialized(declaration)) continue;
                var constructions = declaration.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
                if (constructions.Any(creation => visualNames.Contains(creation.Type.ToString().Split('.').Last())))
                    failures.Add(Path.GetFileName(path));
            }
        }
        Assert.True(failures.Count == 0, "Visual constructors must not register Avalonia global events "
            + "concurrently: " + string.Join(", ", failures));
    }

    private static bool IsTestClass(ClassDeclarationSyntax declaration) => declaration.Members
        .OfType<MethodDeclarationSyntax>().SelectMany(method => method.AttributeLists)
        .SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is "Fact" or "Theory");

    private static bool IsSerialized(ClassDeclarationSyntax declaration) => declaration.AttributeLists
        .SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() == "Collection"
            && attribute.ArgumentList?.Arguments.Count == 1
            && attribute.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.Token.ValueText == "Avalonia visual controls");

    private static string TestSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "tests", "MossAgent.App.Tests");
    }
}
