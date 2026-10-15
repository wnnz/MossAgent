using System.Text.RegularExpressions;
using Avalonia.Controls;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NativeVisualControlInheritanceTests
{
    private static readonly HashSet<string> NativeVisualTypes =
        new[] { typeof(Button).Assembly, typeof(Avalonia.Controls.Shapes.Path).Assembly }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(Control).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Except(["Grid", "StackPanel", "WrapPanel", "Panel", "ContentPresenter"])
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Policy_RejectsNativeVisualBaseTypesOutsideControls()
    {
        var source = "using Native = Avalonia.Controls; "
            + "using Shapes = Avalonia.Controls.Shapes; "
            + "using NativeButton = Avalonia.Controls.Button; "
            + "class First : Button {} class Second : Native.Button {} "
            + "class Third : NativeButton {} class Fourth : Shapes.Path {} "
            + "class Host : UserControl {} class NativeWindow : Window {} "
            + "class Layout : StackPanel {} class Shared : AppButton {} "
            + "class SharedHost : AppUserControl {} class SharedWindow : AppWindow {}";

        Assert.Equal(["Button", "Button", "Button", "Path", "UserControl", "Window"],
            NativeBaseTypes(source));
    }

    [Fact]
    public void AppCode_DoesNotDeriveNativeVisualControlsOutsideWrappers()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var file in Directory.EnumerateFiles(appPath, "*.cs", SearchOption.AllDirectories))
        {
            var parts = Path.GetRelativePath(appPath, file).Split(Path.DirectorySeparatorChar);
            if (parts[0] is "bin" or "obj" or "Controls") continue;

            var offenders = NativeBaseTypes(File.ReadAllText(file));
            Assert.True(offenders.Length == 0,
                $"{Path.GetRelativePath(appPath, file)} derives native visual controls: {string.Join(", ", offenders)}");
        }
    }

    private static string[] NativeBaseTypes(string source)
    {
        var aliases = Regex.Matches(source,
                @"\busing\s+(?<alias>\w+)\s*=\s*(?:global::)?(?<target>Avalonia\.Controls(?:\.\w+)*)\s*;")
            .ToDictionary(match => match.Groups["alias"].Value,
                match => match.Groups["target"].Value, StringComparer.Ordinal);

        return Regex.Matches(source,
                @"\bclass\s+\w+(?:<[^>{}]+>)?\s*:\s*(?:(?<prefix>(?:global::)?Avalonia\.Controls(?:\.\w+)*|\w+)\.)?(?<type>\w+)")
            .Where(match => !match.Groups["prefix"].Success
                || match.Groups["prefix"].Value.Contains("Avalonia.Controls", StringComparison.Ordinal)
                || aliases.ContainsKey(match.Groups["prefix"].Value))
            .Select(match => aliases.TryGetValue(match.Groups["type"].Value, out var target)
                ? target.Split('.').Last() : match.Groups["type"].Value)
            .Where(NativeVisualTypes.Contains)
            .ToArray();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
