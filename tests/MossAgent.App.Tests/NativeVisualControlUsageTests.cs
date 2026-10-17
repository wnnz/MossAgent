using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NativeVisualControlUsageTests
{
    private static readonly HashSet<string> AllowedNativeMarkupElements =
    [
        "Application", "ResourceDictionary", "ResourceInclude", "StyleInclude",
        "Styles", "FluentTheme", "SolidColorBrush", "BoxShadows", "StreamGeometry", "FontFamily",
        "LinearGradientBrush", "GradientStop", "RotateTransform", "TranslateTransform", "Animation", "KeyFrame",
        "Grid", "StackPanel", "WrapPanel", "Panel", "ContentPresenter", "Template", "DataTemplate",
        "ItemsPanelTemplate", "ColumnDefinition", "ControlTemplate", "ControlTheme",
        "Style", "Setter", "KeyBinding", "Binding", "MultiBinding", "TextDecorationCollection", "TextDecoration"
    ];

    private static readonly HashSet<string> NativeVisualTypeNames =
        new[] { typeof(Button).Assembly, typeof(Avalonia.Controls.Shapes.Path).Assembly }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => (typeof(Control).IsAssignableFrom(type)
                    && !AllowedNativeMarkupElements.Contains(type.Name))
                || typeof(FlyoutBase).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Axaml_DoesNotBypassSharedVisualControls()
    {
        foreach (var file in AllMarkupFiles())
        {
            var nativeControls = FindUnapprovedNativeElements(XDocument.Load(file));
            Assert.True(nativeControls.Length == 0,
                $"{Path.GetFileName(file)} directly uses: {string.Join(", ", nativeControls)}");
        }
    }

    [Fact]
    public void BrushAndAnimationPrimitives_AreNotNativeVisualControls()
    {
        var types = new[] { typeof(Avalonia.Media.LinearGradientBrush), typeof(Avalonia.Media.GradientStop),
            typeof(Avalonia.Media.RotateTransform), typeof(Avalonia.Media.TranslateTransform),
            typeof(Avalonia.Animation.Animation), typeof(Avalonia.Animation.KeyFrame) };
        Assert.All(types, type =>
        {
            Assert.Contains(type.Name, AllowedNativeMarkupElements);
            Assert.False(typeof(Control).IsAssignableFrom(type));
        });
    }

    [Theory]
    [InlineData("Styles.Resources")]
    [InlineData("Application.Resources")]
    [InlineData("ResourceDictionary")]
    public void DrawingResourcesAreAllowedWithoutAllowingNativeVisualChildren(string resourceOwner)
    {
        var document = XDocument.Parse($"""
            <Styles xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <{resourceOwner}><DrawingGroup><GeometryDrawing /><Button /></DrawingGroup></{resourceOwner}>
              <Grid><DrawingGroup /><GeometryDrawing /><ui:AppIcon /></Grid>
            </Styles>
            """);
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var query = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedVisualControls")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        Assert.Equal(["Button", "DrawingGroup", "GeometryDrawing"], FindUnapprovedNativeElements(document));
        Assert.Equal(["Button", "DrawingGroup", "GeometryDrawing"], document.XPathSelectElements(query).Select(element => element.Name.LocalName));
        Assert.False(typeof(Control).IsAssignableFrom(typeof(Avalonia.Media.DrawingGroup)));
        Assert.False(typeof(Control).IsAssignableFrom(typeof(Avalonia.Media.GeometryDrawing)));
    }

    [Fact]
    public void FocusTemplates_AllowMarkupFactoriesButStillRejectNativeVisualChildren()
    {
        Assert.False(typeof(Control).IsAssignableFrom(typeof(Avalonia.Markup.Xaml.Templates.Template)));
        var document = XDocument.Parse("""
            <Styles xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <Style Selector="ui|AppButton"><Setter Property="FocusAdorner">
                <Template><ui:AppBorder /><Border /><Button /></Template>
              </Setter></Style>
            </Styles>
            """);
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var query = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedVisualControls")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        Assert.Equal(["Border", "Button"], FindUnapprovedNativeElements(document));
        Assert.Equal(["Border", "Button"], document.XPathSelectElements(query).Select(element => element.Name.LocalName));
    }

    [Fact]
    public void NativeComponentPolicy_RejectsUnlistedVisualControls()
    {
        var document = XDocument.Parse("""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                         xmlns:av="clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls"
                         xmlns:shapes="clr-namespace:Avalonia.Controls.Shapes;assembly=Avalonia.Controls"
                         xmlns:using="using:Avalonia.Controls"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <Grid><Button /><ProgressBar /><Flyout /><av:Button /><shapes:Path />
                <using:TextBox /><ItemsControl /><ContentControl /><UserControl /><Window />
              </Grid>
            </ui:AppUserControl>
            """);

        Assert.Equal(["Button", "ProgressBar", "Flyout", "Button", "Path", "TextBox",
            "ItemsControl", "ContentControl", "UserControl", "Window"],
            FindUnapprovedNativeElements(document));
    }

    [Fact]
    public void BuildGuard_RejectsNativeControlsButAllowsLayoutAndWrappers()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedVisualControls");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var peek = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek");
        var query = Assert.IsType<string>(peek.Attribute("Query")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("bin/**/*.axaml", target.ToString());
        Assert.Contains("obj/**/*.axaml", target.ToString());

        var document = XDocument.Parse("""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls"
                         xmlns:av="clr-namespace:Avalonia.Controls;assembly=Avalonia.Controls">
              <Grid><ui:AppButton /><ui:AppIconButton /><ui:AppActionButton /><Button /><av:TextBox /><StackPanel /><UserControl />
                <MultiBinding><Binding Path="Title" /></MultiBinding>
                <TextDecorationCollection><TextDecoration Location="Underline" /></TextDecorationCollection></Grid>
            </ui:AppUserControl>
            """);
        Assert.Equal(["Button", "TextBox", "UserControl"], document.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));

        var nativeRoot = XDocument.Parse("<UserControl xmlns='https://github.com/avaloniaui' />");
        Assert.Equal(["UserControl"], nativeRoot.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));

        var nativeWindow = XDocument.Parse("<Window xmlns='https://github.com/avaloniaui' />");
        Assert.Equal(["Window"], nativeWindow.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));

        var theme = XDocument.Parse("""
            <ResourceDictionary xmlns="https://github.com/avaloniaui"
                                xmlns:shapes="clr-namespace:Avalonia.Controls.Shapes;assembly=Avalonia.Controls">
              <ControlTheme><ControlTemplate><Border /><shapes:Path /></ControlTemplate></ControlTheme>
            </ResourceDictionary>
            """);
        Assert.Equal(["Border", "Path"], theme.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));
    }

    [Fact]
    public void BuildGuard_RejectsStyledLayoutPrimitives()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateLayoutPrimitivesRemainStructural");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var query = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value;
        Assert.NotNull(query);

        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <Grid Background="Transparent">
                <StackPanel Foreground="Red" />
                <ui:AppBorder Background="Red" />
                <Grid Width="32" />
                <Grid><Grid.Background><SolidColorBrush Color="Red" /></Grid.Background></Grid>
              </Grid>
            </UserControl>
            """);
        Assert.Equal(["Grid", "StackPanel", "Grid.Background"], document.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));
    }

    [Fact]
    public void LayoutMarkup_UsesSharedSurfacesForAppearance()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateLayoutPrimitivesRemainStructural");
        var query = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value;
        Assert.NotNull(query);

        foreach (var file in AllMarkupFiles().Where(path => path.Contains($"{Path.DirectorySeparatorChar}Views{Path.DirectorySeparatorChar}")
            || path.Contains($"{Path.DirectorySeparatorChar}Controls{Path.DirectorySeparatorChar}")))
        {
            Assert.Empty(XDocument.Load(file).XPathSelectElements(query));
        }
    }

    [Fact]
    public void NativeConstructorPolicy_RejectsVisualControls()
    {
        var source = "using Native = Avalonia.Controls; new Button(); "
            + "new Avalonia.Controls.ProgressBar { }; new Native.TextBox(); "
            + "using NativeButton = Avalonia.Controls.Button; new NativeButton(); "
            + "new Avalonia.Controls.Shapes.Path(); new MenuFlyout(); "
            + "new Native.Flyout(); new AppButton(); new AppFlyout();";

        Assert.Equal(["Button", "ProgressBar", "TextBox", "Button", "Path",
            "MenuFlyout", "Flyout"],
            FindNativeVisualConstructors(source));
    }

    [Fact]
    public void NativeConstructorPolicy_RejectsTargetTypedVisualControls()
    {
        var source = "using Native = Avalonia.Controls; "
            + "using NativeButton = Avalonia.Controls.Button; "
            + "Button button = new(); Avalonia.Controls.TextBox input = new(); "
            + "Native.ProgressBar progress = new(); NativeButton action = new(); "
            + "AppButton shared = new(); StackPanel layout = new();";

        Assert.Equal(["Button", "TextBox", "ProgressBar", "Button"],
            FindNativeVisualConstructors(source));
    }

    [Fact]
    public void AppCode_DoesNotConstructNativeVisualControls()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var file in Directory.EnumerateFiles(appPath, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(appPath, file);
            if (relativePath.StartsWith($"obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relativePath.StartsWith($"bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Empty(FindNativeVisualConstructors(File.ReadAllText(file)));
        }
    }

    private static string[] FindNativeVisualConstructors(string source)
    {
        var aliases = Regex.Matches(source,
                @"\busing\s+(?<alias>\w+)\s*=\s*(?:global::)?(?<target>Avalonia\.Controls(?:\.\w+)*)\s*;")
            .ToDictionary(match => match.Groups["alias"].Value,
                match => match.Groups["target"].Value.Split('.').Last(), StringComparer.Ordinal);

        var explicitConstructors = Regex.Matches(source,
                @"\bnew\s+(?:(?<prefix>(?:global::)?Avalonia\.Controls(?:\.\w+)*|\w+)\.)?(?<type>\w+)\s*(?:\(|\{)")
            .Where(match => !match.Groups["prefix"].Success
                || match.Groups["prefix"].Value.StartsWith("Avalonia.Controls", StringComparison.Ordinal)
                || match.Groups["prefix"].Value.StartsWith("global::Avalonia.Controls", StringComparison.Ordinal)
                || aliases.ContainsKey(match.Groups["prefix"].Value))
            .Select(match => aliases.GetValueOrDefault(match.Groups["type"].Value)
                ?? match.Groups["type"].Value)
            .Where(NativeVisualTypeNames.Contains);

        var targetTypedConstructors = Regex.Matches(source,
                @"\b(?:(?<prefix>(?:global::)?Avalonia\.Controls(?:\.\w+)*|\w+)\.)?(?<type>\w+)\s+\w+\s*=\s*new\s*\(")
            .Where(match => !match.Groups["prefix"].Success
                || match.Groups["prefix"].Value.StartsWith("Avalonia.Controls", StringComparison.Ordinal)
                || match.Groups["prefix"].Value.StartsWith("global::Avalonia.Controls", StringComparison.Ordinal)
                || aliases.ContainsKey(match.Groups["prefix"].Value))
            .Select(match => aliases.GetValueOrDefault(match.Groups["type"].Value)
                ?? match.Groups["type"].Value)
            .Where(NativeVisualTypeNames.Contains);

        return explicitConstructors.Concat(targetTypedConstructors).ToArray();
    }

    private static string[] FindUnapprovedNativeElements(XDocument document) =>
        document.Root!.DescendantsAndSelf()
            .Where(element => (element.Name.NamespaceName == "https://github.com/avaloniaui"
                    || element.Name.NamespaceName.StartsWith("clr-namespace:Avalonia.Controls", StringComparison.Ordinal)
                    || element.Name.NamespaceName.StartsWith("using:Avalonia.Controls", StringComparison.Ordinal))
                && !element.Name.LocalName.Contains('.')
                && !AllowedNativeMarkupElements.Contains(element.Name.LocalName)
                && !((element.Name.LocalName is "DrawingGroup" or "GeometryDrawing")
                    && element.Ancestors().Any(ancestor => ancestor.Name.LocalName is "Styles.Resources" or "Application.Resources" or "ResourceDictionary")))
            .Select(element => element.Name.LocalName)
            .ToArray();

    private static IEnumerable<string> AllMarkupFiles()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        return Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(appPath, file).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"));
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
