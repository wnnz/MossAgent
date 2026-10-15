using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedStyleClassMarkupTests
{
    [Theory]
    [InlineData("<ui:AppInput Classes.app-input='False' />")]
    [InlineData("<ui:AppCheckBox Classes.app-check-box='{Binding Enabled}' />")]
    [InlineData("<ui:AppComboBox StyledElement.Classes.app-combo-box='False' />")]
    [InlineData("<ui:AppInput Classes='app-input' />")]
    [InlineData("<ui:AppInput StyledElement.Classes='app-input' />")]
    [InlineData("<ui:AppInput ui:AppInput.Classes='field-multiline app-input' />")]
    [InlineData("<ui:AppInput Classes='field-multiline app-input' />")]
    [InlineData("<ui:AppInput Classes='field-multiline&#x9;app-input' />")]
    [InlineData("<ui:AppInput><Classes.app-input>False</Classes.app-input></ui:AppInput>")]
    [InlineData("<ui:AppInput><ui:AppInput.Classes>app-input</ui:AppInput.Classes></ui:AppInput>")]
    [InlineData("<ui:AppInput><StyledElement.Classes>field-multiline app-input</StyledElement.Classes></ui:AppInput>")]
    public void BusinessMarkup_CannotRemoveOrImpersonateSharedStyleIdentity(string markup)
    {
        Assert.Single(Document(markup).XPathSelectElements(Query()));
    }

    [Fact]
    public void SharedVariantsAndStateBindings_RemainAllowed()
    {
        var document = Document("""
            <Grid Classes="project-navigation-row" Classes.selected="{Binding IsSelected}">
              <ui:AppInput Classes="field-multiline" Text="{Binding Draft}" />
              <ui:AppActionButton Classes="ghost" Classes.selected="{Binding IsSelected}" />
              <ui:AppText Classes="subtle" Classes.status-error="{Binding HasError}" Text="{Binding Status}" />
            </Grid>
            """);

        Assert.Empty(document.XPathSelectElements(Query()));
    }

    [Fact]
    public void BuildGuard_CoversBusinessMarkupAndPreservesSharedImplementationOwnership()
    {
        var target = Target();
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var scope = Assert.Single(target.Elements("ItemGroup")).Element("_StyleClassIdentityMarkup");
        Assert.NotNull(scope);
        Assert.Equal("$(MSBuildProjectDirectory)/**/*.axaml", scope.Attribute("Include")?.Value);
        var exclusions = Assert.IsType<string>(scope.Attribute("Exclude")?.Value).Split(';');
        Assert.Equal(["$(MSBuildProjectDirectory)/App.axaml", "$(MSBuildProjectDirectory)/Styles/**/*.axaml",
            "$(MSBuildProjectDirectory)/Controls/**/*.axaml", "$(MSBuildProjectDirectory)/bin/**/*.axaml",
            "$(MSBuildProjectDirectory)/obj/**/*.axaml"], exclusions);
        Assert.NotNull(target.Element("Error"));
    }

    [Fact]
    public void AllBusinessMarkup_PreservesSharedStyleIdentity()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var query = Query();
        foreach (var file in Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(appPath, file);
            if (relative == "App.axaml"
                || relative.Split(Path.DirectorySeparatorChar)[0] is "Styles" or "Controls" or "bin" or "obj")
                continue;
            Assert.True(!XDocument.Load(file).XPathSelectElements(query).Any(),
                $"{relative} overrides shared style identity.");
        }
    }

    private static XDocument Document(string content) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>"
        + content + "</Grid>");

    private static string Query() => Assert.IsType<string>(
        Assert.Single(Target().Elements("XmlPeek")).Attribute("Query")?.Value);

    private static XElement Target() => Assert.Single(XDocument.Load(Path.Combine(RepositoryRoot(), "src",
        "MossAgent.App", "MossAgent.App.csproj")).Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedStyleClassIdentity");

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
