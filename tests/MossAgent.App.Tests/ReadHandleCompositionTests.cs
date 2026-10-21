using Microsoft.Extensions.DependencyInjection;
using MossAgent.Domain;
using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ReadHandleCompositionTests
{
    [Fact]
    public async Task ProductionExecutor_UsesWindowsGuardOverCallerGuard_AndPersistsFailure()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n");
        Assert.IsType<WindowsFileReadHandleValidator>(fixture.Services.GetRequiredService<IFileReadHandleValidator>());
        fixture.Start(ApprovalPolicy.FullAccess);
        await fixture.WaitForCompletionAsync();
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        var executor = fixture.Services.GetRequiredService<IToolExecutor>();
        var context = new ToolExecutionContext(fixture.TaskId, fixture.ProjectRoot,
            [fixture.ProjectRoot], ApprovalPolicy.ReadOnly) { ReadHandleValidator = new RefusingReadHandleValidator() };
        var request = new ToolRequest("native-guard-wins", "filesystem.read_file",
            System.Text.Json.JsonSerializer.SerializeToElement(new { path = fixture.FilePath }));
        var result = await executor.ExecuteAsync(request, context, fixture.Token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("beta\n", result.Content);

        var missingRoot = Path.Combine(fixture.Root, "missing-root");
        var safeSnapshot = await FileSnapshotReader.ReadAsync(fixture.FilePath, fixture.Token);
        var snapshotRequest = new ToolRequest("native-handle-denied", "filesystem.read_file", request.Arguments);
        var guardError = Assert.Throws<UnauthorizedAccessException>(() =>
        {
            using var opened = FileSnapshotReader.Open(fixture.FilePath,
                context with { AuthorizedRoots = [missingRoot], ReadHandleValidator = fixture.Services.GetRequiredService<IFileReadHandleValidator>() });
        });
        Assert.DoesNotContain(fixture.Root, guardError.Message, StringComparison.OrdinalIgnoreCase);
        var denied = await executor.ExecuteAsync(snapshotRequest, context with { AuthorizedRoots = [] }, fixture.Token);
        Assert.Equal("path_denied", denied.ErrorCode);
        var audits = await fixture.Services.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(fixture.TaskId, 10, fixture.Token);
        Assert.True(Assert.Single(audits, entry => entry.CallId == request.CallId).IsSuccess);
        var failure = Assert.Single(audits, entry => entry.CallId == snapshotRequest.CallId);
        Assert.False(failure.IsSuccess);
        Assert.Equal("path_denied", failure.ErrorCode);
        Assert.NotNull(failure.CompletedAt);
        Assert.Equal(safeSnapshot, await FileSnapshotReader.ReadAsync(fixture.FilePath, fixture.Token));
    }
}
