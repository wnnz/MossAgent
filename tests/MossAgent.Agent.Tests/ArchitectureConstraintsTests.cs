using System.Text.RegularExpressions;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed partial class ArchitectureConstraintsTests
{
    [Fact]
    public void HandwrittenCSharpFiles_ContainAtMostOneTypeAndStayUnderLimit()
    {
        var root = FindRepositoryRoot();
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        var failures = new List<string>();

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var typeCount = lines.Count(line => TypeDeclarationRegex().IsMatch(line));
            if (typeCount > 1 || lines.Length > 500)
            {
                failures.Add($"{Path.GetRelativePath(root, file)}: types={typeCount}, lines={lines.Length}");
            }
        }

        Assert.Empty(failures);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MossAgent.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("找不到 MossAgent 仓库根目录。");
    }

    [GeneratedRegex(@"^\s*(public|internal|private|protected|file)?\s*(sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+)*(class|record|struct|interface|enum|delegate)\s+")]
    private static partial Regex TypeDeclarationRegex();
}
