using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class HeadlessDispatchUsageTests
{
    [Fact]
    public void AsyncUiCallbacks_UseAnAwaitingDispatchEntryPoint()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var testRoot = Path.Combine(directory.FullName, "tests", "MossAgent.App.Tests");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken);
            foreach (var invocation in tree.GetRoot(TestContext.Current.CancellationToken)
                .DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax
                    { Name: IdentifierNameSyntax { Identifier.ValueText: "Dispatch" } }
                    || invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression
                        is not AnonymousFunctionExpressionSyntax { AsyncKeyword.RawKind: not 0 }) continue;
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                offenders.Add($"{Path.GetRelativePath(testRoot, file)}:{line}");
            }
        }
        Assert.True(offenders.Count == 0,
            "Use DispatchAsync or an explicit generic asynchronous result instead of an async Action: "
            + string.Join(", ", offenders));
    }
}
