using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class BrowserToolbarPipelineTests
{
    public static TheoryData<string, ApprovalPolicy, bool> Policies
    {
        get
        {
            var data = new TheoryData<string, ApprovalPolicy, bool>();
            foreach (var action in new[] { "navigate", "back", "forward", "reload", "new_tab", "close_tab", "switch_tab", "devtools" })
            {
                data.Add(action, ApprovalPolicy.FullAccess, true);
                data.Add(action, ApprovalPolicy.ReadOnly, false);
                data.Add(action, ApprovalPolicy.AskEveryTime, true);
                data.Add(action, ApprovalPolicy.AskEveryTime, false);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Policies))]
    public async Task ToolbarActions_UsePolicyAndAudit(string action, ApprovalPolicy policy, bool allow)
    {
        using var fixture = new BrowserToolbarFixture(policy);
        fixture.Approve = _ => Task.FromResult(allow);
        await fixture.RunAsync(action);

        var started = Assert.Single(fixture.Audit.Starts);
        Assert.Equal(fixture.Context.TaskId, started.TaskId);
        Assert.Equal("browser." + action, started.ToolName);
        Assert.Equal(policy, started.ApprovalPolicy);
        Assert.Equal(allow, Assert.Single(fixture.Audit.Completions).IsSuccess);
        if (allow) Assert.Equal(action, Assert.Single(fixture.Session.Calls));
        else
        {
            Assert.Empty(fixture.Session.Calls);
            Assert.Equal("当前策略或用户拒绝了浏览器操作。", fixture.Pane.Status);
        }
        if (policy == ApprovalPolicy.AskEveryTime)
            Assert.Same(fixture.Session, Assert.Single(fixture.ApprovalContexts).BrowserSession);
        else Assert.Empty(fixture.ApprovalContexts);
        Assert.False(fixture.Pane.Actions.IsBusy);
    }

    [Theory]
    [InlineData("file:///C:/secret")]
    [InlineData("ftp://fixture.invalid/file")]
    [InlineData("")]
    public async Task InvalidAddress_DoesNotEnterPipeline(string address)
    {
        using var fixture = new BrowserToolbarFixture();
        fixture.Pane.Address = address;
        await fixture.RunAsync("navigate");

        Assert.Empty(fixture.Session.Calls);
        Assert.Empty(fixture.Audit.Starts);
        Assert.Equal("请输入有效的 http 或 https 地址。", fixture.Pane.Status);
    }

    [Fact]
    public async Task Navigation_NormalizesAddressWithoutTouchingNativeView()
    {
        using var fixture = new BrowserToolbarFixture();
        fixture.Pane.Address = "example.invalid/path";
        await fixture.RunAsync("navigate");

        Assert.Equal(new Uri("https://example.invalid/path"), fixture.Session.CurrentUri);
        Assert.Equal(new Uri("https://fixture.invalid/second"), fixture.View.CurrentUrl);
    }

    [Fact]
    public async Task ExecutionFailure_DoesNotExposeExceptionSecrets()
    {
        using var fixture = new BrowserToolbarFixture();
        fixture.Session.Perform = (_, _) => Task.FromException(new InvalidOperationException("API_KEY=private-cookie"));
        await fixture.RunAsync("reload");

        Assert.Equal("浏览器操作失败，请检查会话状态后重试。", fixture.Pane.Status);
        Assert.Equal("tool_failed", Assert.Single(fixture.Audit.Completions).ErrorCode);
    }
}
