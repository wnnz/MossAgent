using System.Xml.Linq;

namespace MossAgent.App.Tests;

internal static class AppBuildDefinition
{
    public static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
                    return directory.FullName;
            }
            throw new DirectoryNotFoundException("Cannot locate the MossAgent solution.");
        }
    }

    public static XDocument Load(string? projectPath = null)
    {
        projectPath ??= Path.Combine(RepositoryRoot, "src", "MossAgent.App", "MossAgent.App.csproj");
        var project = XDocument.Load(projectPath);
        foreach (var import in project.Root!.Elements("Import").ToArray())
        {
            var relative = import.Attribute("Project")?.Value
                ?? throw new InvalidOperationException("A build import must specify its project.");
            if (import.Attribute("Condition") is not null || relative.IndexOfAny(['$', '*', '?']) >= 0)
                throw new InvalidOperationException("Use MSBuild to evaluate conditional or dynamic imports.");
            var imported = XDocument.Load(Path.Combine(Path.GetDirectoryName(projectPath)!, relative));
            if (imported.Root!.Elements("Import").Any())
                throw new InvalidOperationException("Nested build imports require MSBuild evaluation.");
            import.ReplaceWith(imported.Root.Elements().Select(element => new XElement(element)).ToArray());
        }
        return project;
    }
}
