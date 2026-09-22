using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceApprovalPolicyTests
{
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
        Assert.Contains(expectedLabel, workspace.ApprovalPolicyLabel);
    }
}
