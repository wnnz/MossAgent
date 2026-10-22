using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WindowsManifestTests
{
    [Fact]
    public void AppBuild_EmbedsWindowsCompatibilityManifest()
    {
        var project = AppBuildDefinition.Load();
        var manifest = Assert.Single(project.Descendants("ApplicationManifest"));

        Assert.Equal("app.manifest", manifest.Value);
        Assert.True(File.Exists(Path.Combine(AppPath(), manifest.Value)));
    }

    [Fact]
    public void NativeHosts_UseSupportedWindowsAndOrdinaryPrivileges()
    {
        var manifest = XDocument.Load(Path.Combine(AppPath(), "app.manifest"));
        XNamespace compatibility = "urn:schemas-microsoft-com:compatibility.v1";
        XNamespace privileges = "urn:schemas-microsoft-com:asm.v3";
        var supported = Assert.Single(manifest.Descendants(compatibility + "supportedOS"));
        var execution = Assert.Single(manifest.Descendants(privileges + "requestedExecutionLevel"));

        Assert.Equal("{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}", supported.Attribute("Id")?.Value);
        Assert.Equal("asInvoker", execution.Attribute("level")?.Value);
        Assert.Equal("false", execution.Attribute("uiAccess")?.Value);
    }

    [Fact]
    public void NativeHosts_DeclarePerMonitorDpiCompatibility()
    {
        var manifest = XDocument.Load(Path.Combine(AppPath(), "app.manifest"));
        XNamespace modern = "http://schemas.microsoft.com/SMI/2016/WindowsSettings";
        XNamespace fallback = "http://schemas.microsoft.com/SMI/2005/WindowsSettings";

        Assert.Equal("PerMonitorV2,PerMonitor", Assert.Single(manifest.Descendants(modern + "dpiAwareness")).Value);
        Assert.Equal("true/pm", Assert.Single(manifest.Descendants(fallback + "dpiAware")).Value);
    }

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
