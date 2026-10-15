using MossAgent.App.Controls;
using Xunit;
using WebViewControl = NativeWebView.Controls.NativeWebView;

namespace MossAgent.App.Tests;

public sealed class EmbeddedBrowserComponentTests
{
    [Fact]
    public void EmbeddedBrowser_UsesSharedVisualWrapper()
    {
        Assert.Equal(typeof(WebViewControl), typeof(AppEmbeddedBrowserView).BaseType);

        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        Assert.Contains("new AppEmbeddedBrowserView(backend)", File.ReadAllText(
            Path.Combine(appPath, "Browser", "EmbeddedBrowserSessionFactory.cs")));
        foreach (var file in Directory.EnumerateFiles(appPath, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(appPath, file).Split(Path.DirectorySeparatorChar);
            if (relative[0] is "Controls" or "bin" or "obj") continue;

            var source = File.ReadAllText(file);
            Assert.DoesNotMatch(@"\bnew\s+(?:WebViewControl|(?:NativeWebView\.Controls\.)?NativeWebView)\s*\(",
                source);
        }
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
