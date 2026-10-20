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
                if (!IsUnsafeDispatch(invocation)) continue;
                var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                offenders.Add($"{Path.GetRelativePath(testRoot, file)}:{line}");
            }
        }
        Assert.True(offenders.Count == 0,
            "Use DispatchAsync or an explicit generic asynchronous result instead of an async Action or Task result: "
            + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("session.Dispatch<Task>(async () => { await Work(); }, token)", true)]
    [InlineData("session.Dispatch<System.Threading.Tasks.Task<bool>>(async () => await Work(), token)", true)]
    [InlineData("session.Dispatch(async () => await Work(), token)", true)]
    [InlineData("session.Dispatch<Task>(() => CleanupWithoutContext(), token)", false)]
    [InlineData("session.Dispatch<bool>(async () => { await Work(); return true; }, token)", false)]
    [InlineData("session.DispatchAsync(async () => await Work(), token)", false)]
    public void DetectionDistinguishesTaskResultsFromAwaitedCallbacks(string expression, bool expected)
    {
        var invocation = Assert.IsType<InvocationExpressionSyntax>(SyntaxFactory.ParseExpression(expression));
        Assert.Equal(expected, IsUnsafeDispatch(invocation));
    }

    private static bool IsUnsafeDispatch(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression
            is not AnonymousFunctionExpressionSyntax { AsyncKeyword.RawKind: not 0 }) return false;
        return invocation.Expression is MemberAccessExpressionSyntax
                { Name: IdentifierNameSyntax { Identifier.ValueText: "Dispatch" } }
            || invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic }
                && generic.Identifier.ValueText == "Dispatch"
                && generic.TypeArgumentList.Arguments.Any(argument => argument.DescendantTokens().Any(token => token.ValueText == "Task"));
    }
}
