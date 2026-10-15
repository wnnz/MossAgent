using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ArchitectureConstraintsTests
{
    [Fact]
    public void HandwrittenCSharpFiles_ContainExactlyOneTypeAndStayUnderLimit()
    {
        var root = FindRepositoryRoot();
        var files = new[] { "src", "tests" }
            .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        var failures = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var typeCount = CountTypes(string.Join(Environment.NewLine, lines));
            if (typeCount != 1 || lines.Length > 500)
            {
                failures.Add($"{Path.GetRelativePath(root, file)}: types={typeCount}, lines={lines.Length}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("class Real { string fixture = \"class Example { }\"; }", 1)]
    [InlineData("class Real { string fixture = \"\"\"class First { } class Second { }\"\"\"; }", 1)]
    [InlineData("// class Ignored { }\nclass Real { }", 1)]
    [InlineData("class First { } class Second { }", 2)]
    [InlineData("class Outer { private class Nested { } }", 2)]
    [InlineData("record Entry(); struct Value { } interface Contract { } enum Kind { Item } delegate void Callback();", 5)]
    public void TypeCounting_UsesDeclarationsInsteadOfFixtureText(string source, int expected) =>
        Assert.Equal(expected, CountTypes(source));

    private static int CountTypes(string source) => CSharpSyntaxTree.ParseText(source).GetRoot()
        .DescendantNodes().Count(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MossAgent.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("找不到 MossAgent 仓库根目录。");
    }

}
