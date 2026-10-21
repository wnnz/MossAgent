using System.Text.Json;
using MossAgent.Browser.Tools;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class BrowserArtifactFlowFixture : IDisposable
{
    public OwnedArtifactFixture Files { get; } = new();
    public BrowserArtifactTestSession Browser { get; } = new();
    public ObservedArtifactSessions Sessions { get; set; } = new();
    public int Approvals { get; private set; }
    public ToolExecutionContext Context => Files.Context with { BrowserSession = Browser, ApprovalPolicy = ApprovalPolicy.AskEveryTime };

    public Task<ToolResult> ExecuteAsync(bool download, ToolExecutionContext? context = null, CancellationToken? token = null,
        TimeProvider? time = null, bool approve = true)
    {
        IAgentTool tool = download ? new BrowserDownloadTool(Sessions) : new BrowserScreenshotTool(Sessions);
        var executor = new ToolExecutor(new([tool]), new PolicyApprovalService((_, _, _) =>
        {
            Approvals++;
            return ValueTask.FromResult(approve);
        }), timeProvider: time);
        var arguments = download ? JsonSerializer.SerializeToElement(new { selector = "#download" }) : JsonSerializer.SerializeToElement(new { fullPage = true });
        return executor.ExecuteAsync(new("browser-artifact", tool.Descriptor.Name, arguments), context ?? Context, token ?? Files.Token);
    }

    public void Dispose() => Files.Dispose();
}
