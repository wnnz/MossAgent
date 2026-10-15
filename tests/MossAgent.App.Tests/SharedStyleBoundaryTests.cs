using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedStyleBoundaryTests
{
    [Fact]
    public void ViewsAndReusableControls_DoNotDeclareLocalStyles()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var folder in new[] { "Views", "Controls" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, folder), "*.axaml",
                SearchOption.AllDirectories))
            {
                var document = XDocument.Load(file);
                Assert.DoesNotContain(document.Descendants(), element =>
                    element.Name.LocalName is "Style" or "ControlTheme");
            }
        }
    }

    [Fact]
    public void BuildGuard_RejectsLocalStyles()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateNoViewLocalStyles");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("Styles/**/*.axaml", target.ToString());
        var query = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value;
        Assert.NotNull(query);

        var markup = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <UserControl.Styles><Style Selector="ui|AppButton" /></UserControl.Styles>
              <UserControl.Resources><ControlTheme TargetType="ui:AppButton" /></UserControl.Resources>
              <ui:AppButton />
            </UserControl>
            """);
        Assert.Equal(["Style", "ControlTheme"], markup.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));
    }

    [Fact]
    public void SidebarAndChatInteractionVariants_LiveInSharedStyles()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var variants = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        Assert.Contains(variants.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/NavigationActionVariants.axaml");
        var navigation = XDocument.Load(Path.Combine(appPath, "Styles", "NavigationActionVariants.axaml"));
        var response = XDocument.Load(Path.Combine(appPath, "Styles", "ResponseContentVariants.axaml"));
        var selectors = variants.Descendants().Concat(navigation.Descendants()).Concat(response.Descendants())
            .Where(element => element.Name.LocalName == "Style")
            .SelectMany(element => (element.Attribute("Selector")?.Value ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("Grid.response-text-block.body", selectors);
        Assert.DoesNotContain("controls|AppItemsControl.chat-blocks ContentPresenter", selectors);
        Assert.Contains("controls|AppBorder.directory-row:pointerover controls|AppButton.set-primary", selectors);
        Assert.Contains("Grid.task-navigation-row controls|AppButton.task-options", selectors);
        Assert.Contains("Grid.task-navigation-row:pointerover controls|AppButton.task-options", selectors);
        Assert.Contains("Grid.task-navigation-row:focus-within controls|AppButton.task-options", selectors);
        Assert.Contains("Grid.project-navigation-row:pointerover controls|AppButton.project-toggle", selectors);
        Assert.Contains("Grid.project-navigation-row:focus-within controls|AppButton.project-toggle", selectors);
        Assert.Contains("controls|AppBorder.projects-section-header:focus-within controls|AppButton.project-header-action", selectors);

        var chat = XDocument.Load(Path.Combine(appPath, "Controls", "AppResponseContent.axaml"));
        Assert.Contains(chat.Descendants(), element => element.Name.LocalName == "AppItemsControl"
            && element.Attribute("Classes")?.Value == "chat-blocks");
    }

    [Fact]
    public void SharedButtonStyleKey_HasNoUnreachableSubtypeSelectors()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var styles = Directory.EnumerateFiles(Path.Combine(appPath, "Styles"), "*.axaml")
            .Append(Path.Combine(appPath, "App.axaml"));
        var selectors = styles.SelectMany(file => XDocument.Load(file).Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value))
            .Where(selector => selector is not null)
            .ToArray();

        Assert.DoesNotContain(selectors, selector => selector!.Contains("controls|AppIconButton", StringComparison.Ordinal)
            || selector.Contains("controls|AppActionButton", StringComparison.Ordinal));
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
