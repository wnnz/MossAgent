using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.Tools.Tests;

internal sealed class McpToolRegistrationFixture : IAsyncDisposable
{
    public McpToolRegistrationFixture()
    {
        var paths = new AppDataPaths(Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N")));
        Manager = new McpManager(Configurations, new McpHttpClientFactory(
            new SqliteConfigurationRepository(new SqliteConnectionFactory(paths))), Tools, new RejectingTaskProcessLauncher());
        var audit = new ToolProgressAuditSink();
        audit.Release.TrySetResult();
        Executor = new ToolExecutor(Tools, new PolicyApprovalService((_, _, _) => ValueTask.FromResult(false)), audit);
    }

    public McpRegistrationConfigurationRepository Configurations { get; } = new();
    public ToolRegistry Tools { get; } = new([new ApprovalExecutionProbeTool()]);
    public McpManager Manager { get; }
    public ToolExecutor Executor { get; }

    public McpServerProfile AddProfile(string name, Uri endpoint)
    {
        var profile = new McpServerProfile(Guid.NewGuid(), name, McpTransportKind.StreamableHttp,
            null, "[]", null, "{}", endpoint, "{}", null, true);
        Configurations.Profiles.Add(profile);
        return profile;
    }

    public IReadOnlyList<ToolDescriptor> Descriptors => Tools.Descriptors.Where(tool => tool.Name.StartsWith("mcp.", StringComparison.Ordinal)).ToArray();

    public Task<ToolResult> ExecuteAsync(string name, ApprovalPolicy policy = ApprovalPolicy.FullAccess) => Executor.ExecuteAsync(
        new ToolRequest(Guid.NewGuid().ToString("N"), name, JsonSerializer.SerializeToElement(new { text = "sample" })),
        new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [], policy), Xunit.TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => Manager.DisposeAsync();
}
