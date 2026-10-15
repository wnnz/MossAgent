using System.Xml.Linq;
using Xunit;

namespace MossAgent.Browser.Tests;

public sealed class WindowsPackagingTests
{
    [Fact]
    public void SingleFileProfile_BundlesAllRuntimeAssetsWithoutTrimming()
    {
        var profile = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Properties", "PublishProfiles", "WindowsX64.pubxml"));

        Assert.Equal("win-x64", Assert.Single(profile.Descendants("RuntimeIdentifier")).Value);
        foreach (var property in new[] { "SelfContained", "PublishSingleFile", "IncludeAllContentForSelfExtract",
                     "EnableCompressionInSingleFile" })
            Assert.Equal("true", Assert.Single(profile.Descendants(property)).Value);
        Assert.Equal("false", Assert.Single(profile.Descendants("PublishTrimmed")).Value);
        Assert.Equal("false", Assert.Single(profile.Descendants("PublishAot")).Value);
        Assert.Equal("false", Assert.Single(profile.Descendants("DebugSymbols")).Value);
    }

    [Fact]
    public void PublishProfiles_ContainOnlyWindowsSingleFile()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Properties", "PublishProfiles");
        Assert.Equal(["WindowsX64.pubxml"], Directory.GetFiles(directory, "*.pubxml")
            .Select(path => new FileInfo(path).Name).Order().ToArray());
    }

    [Fact]
    public void NormalBuild_LeavesPackagingOptionsInPublishProfile()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var global = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Build.props"));
        foreach (var property in new[] { "PublishAot", "PublishTrimmed", "PublishSingleFile", "SelfContained" })
        {
            Assert.Empty(project.Descendants(property));
            Assert.Empty(global.Descendants(property));
        }
    }

    [Fact]
    public void ProductionPublish_RemovesNativeDebugSymbolsBeforeBundlingAndCopying()
    {
        var targets = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Build.targets"));
        var target = Assert.Single(targets.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ExcludeDesktopPublishSymbols");

        Assert.Equal("_ComputeFilesToBundle;CopyFilesToPublishDirectory", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("'$(DebugSymbols)' == 'false'", Assert.IsType<string>(target.Attribute("Condition")?.Value));
        Assert.Contains("'.pdb'", Assert.IsType<string>(Assert.Single(target.Descendants("ResolvedFileToPublish"))
            .Attribute("Condition")?.Value));
    }

    [Fact(Timeout = 30000)]
    public async Task WindowsDriver_StartsAndLoadsSharedPackage()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        Assert.Equal("chromium", playwright.Chromium.Name);
        Assert.Equal("firefox", playwright.Firefox.Name);
        Assert.Equal("webkit", playwright.Webkit.Name);
    }

    [Fact]
    public void DesktopApplication_TargetsWindowsX64()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));

        Assert.Equal("win-x64", Assert.Single(project.Descendants("RuntimeIdentifier")).Value);
        Assert.Equal("x64", Assert.Single(project.Descendants("PlatformTarget")).Value);
    }

    [Fact]
    public void PlaywrightPlatform_IsExplicitForAllBuilds()
    {
        var properties = XDocument.Load(Path.Combine(RepositoryRoot(), "Directory.Build.props"));

        Assert.Equal("win-x64", Assert.Single(properties.Descendants("PlaywrightPlatform")).Value);
    }

    [Fact]
    public void DriverOutput_ContainsOnlyWindowsX64NodeAndLicense()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, ".playwright", "node");

        Assert.Equal(["win32_x64"], Directory.GetDirectories(directory).Select(path => new DirectoryInfo(path).Name).Order().ToArray());
        Assert.True(File.Exists(Path.Combine(directory, "win32_x64", "node.exe")));
        Assert.True(File.Exists(Path.Combine(directory, "LICENSE")));
    }

    [Fact]
    public void DriverOutput_PreservesSharedPackageWithoutOtherPlatformInstallers()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, ".playwright", "package");
        var installers = Directory.GetFiles(Path.Combine(directory, "bin")).Select(Path.GetFileName).ToArray();

        Assert.DoesNotContain(installers, name => name is not null
            && (name.EndsWith("_linux.sh", StringComparison.Ordinal)
                || name.EndsWith("_mac.sh", StringComparison.Ordinal)
                || name == "install_webkit_wsl.ps1"));
        Assert.Contains("reinstall_chrome_stable_win.ps1", installers);
        Assert.Contains("reinstall_msedge_stable_win.ps1", installers);
        Assert.True(File.Exists(Path.Combine(directory, "cli.js")));
        Assert.True(File.Exists(Path.Combine(directory, "browsers.json")));
        Assert.True(File.Exists(Path.Combine(directory, "LICENSE")));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
