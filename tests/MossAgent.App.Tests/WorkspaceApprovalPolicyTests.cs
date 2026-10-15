using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceApprovalPolicyTests
{
    [Fact]
    public void ComposerPolicyIndicator_UsesSharedGeometryInsteadOfEmoji()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);

        var document = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Views", "WorkspaceComposerView.axaml"));
        var policyButton = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "AppApprovalPolicyButton"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                && attribute.Value == "ApprovalPolicyButton"));
        Assert.Equal("{Binding ApprovalPolicy}", policyButton.Attribute("Policy")?.Value);
        Assert.Equal("{Binding SetApprovalPolicyCommand}", policyButton.Attribute("SelectPolicyCommand")?.Value);
        Assert.Empty(policyButton.Elements());
        var styles = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Styles", "WorkspacePromptVariants.axaml"));
        foreach (var geometry in new[] { "UiIconLock", "UiIconShield", "UiIconWarning" })
            Assert.Contains(styles.Descendants(), element => element.Attribute("Property")?.Value == "IconData"
                && element.Attribute("Value")?.Value == "{StaticResource " + geometry + "}");
    }

    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly, "只读模式")]
    [InlineData(ApprovalPolicy.AskEveryTime, "每次询问")]
    [InlineData(ApprovalPolicy.FullAccess, "完全访问")]
    public async Task SetApprovalPolicyCommand_SelectsRequestedPolicy(
        ApprovalPolicy policy,
        string expectedLabel)
    {
        await using var services = AppComposition.CreateServices();
        var workspace = services.GetRequiredService<WorkspaceViewModel>();

        workspace.SetApprovalPolicyCommand.Execute(policy);

        Assert.Equal(policy, workspace.ApprovalPolicy);
        Assert.Equal(expectedLabel, workspace.ApprovalPolicyLabel);
        Assert.Equal(policy == ApprovalPolicy.ReadOnly, workspace.IsReadOnlyApprovalPolicy);
        Assert.Equal(policy == ApprovalPolicy.AskEveryTime, workspace.IsAskEveryTimeApprovalPolicy);
        Assert.Equal(policy == ApprovalPolicy.FullAccess, workspace.IsFullAccessApprovalPolicy);
    }
}
