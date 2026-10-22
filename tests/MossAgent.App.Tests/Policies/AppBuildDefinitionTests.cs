using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppBuildDefinitionTests
{
    [Fact]
    public void ImportsPreserveTargetOrderAndProjectMetadata()
    {
        var directory = Directory.CreateTempSubdirectory("mossagent-build-import-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "guard.targets"),
                "<Project><Target Name='ImportedGuard' BeforeTargets='CoreCompile' /></Project>");
            var project = Path.Combine(directory.FullName, "app.csproj");
            File.WriteAllText(project, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <Target Name="Before" />
                  <Import Project="guard.targets" />
                  <Target Name="After" />
                </Project>
                """);

            var definition = AppBuildDefinition.Load(project);

            Assert.Equal("net10.0", definition.Descendants("TargetFramework").Single().Value);
            Assert.Equal(["Before", "ImportedGuard", "After"], definition.Descendants("Target")
                .Select(target => target.Attribute("Name")!.Value));
            Assert.Equal("CoreCompile", definition.Descendants("Target")
                .Single(target => target.Attribute("Name")?.Value == "ImportedGuard").Attribute("BeforeTargets")?.Value);
        }
        finally { directory.Delete(recursive: true); }
    }

    [Theory]
    [InlineData("<Import Project='missing.targets' />", typeof(FileNotFoundException))]
    [InlineData("<Import Project='*.targets' />", typeof(InvalidOperationException))]
    [InlineData("<Import Project='$(GuardFile)' />", typeof(InvalidOperationException))]
    [InlineData("<Import Project='guard.targets' Condition='false' />", typeof(InvalidOperationException))]
    public void MissingOrUnevaluatedGuardsDoNotSilentlyDisappear(string import, Type expectedException)
    {
        var directory = Directory.CreateTempSubdirectory("mossagent-invalid-import-");
        try
        {
            var project = Path.Combine(directory.FullName, "app.csproj");
            File.WriteAllText(project, $"<Project>{import}</Project>");

            var exception = Record.Exception(() => AppBuildDefinition.Load(project));

            Assert.IsType(expectedException, exception);
        }
        finally { directory.Delete(recursive: true); }
    }
}
