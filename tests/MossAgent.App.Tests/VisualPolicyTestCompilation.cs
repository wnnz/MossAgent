using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

internal static class VisualPolicyTestCompilation
{
    public static async Task<ImmutableArray<Diagnostic>> Analyze(string source, DiagnosticAnalyzer analyzer,
        ImmutableDictionary<string, ReportDiagnostic>? diagnosticOptions = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Append(typeof(AppButton).Assembly)
            .Append(typeof(Avalonia.Controls.Control).Assembly)
            .Append(typeof(Avalonia.Data.Binding).Assembly)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create("VisualPolicyTest", [syntaxTree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions(diagnosticOptions ?? ImmutableDictionary<string, ReportDiagnostic>.Empty));
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
        return await compilation.WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }
}
