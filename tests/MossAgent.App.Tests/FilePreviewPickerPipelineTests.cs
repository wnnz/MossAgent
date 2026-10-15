using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FilePreviewPickerPipelineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PickerNeverGrantsAccessButUsesExistingMultipleAuthorizedRoots(bool authorizeSecondary)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent-preview-picker-" + Guid.NewGuid().ToString("N"));
        var allowed = Directory.CreateDirectory(Path.Combine(root, "primary")).FullName;
        var secondary = Directory.CreateDirectory(Path.Combine(root, "secondary")).FullName;
        var initial = Path.Combine(allowed, "existing.cs");
        var selected = Path.Combine(secondary, "selected.cs");
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(initial, "original authorized content", token);
        await File.WriteAllTextAsync(selected, "synthetic secondary content", token);
        try
        {
            var artifacts = new TaskArtifactPaths(new AppDataPaths(Path.Combine(root, "data")));
            var taskId = Guid.NewGuid();
            var context = new ToolExecutionContext(taskId, allowed, authorizeSecondary ? [allowed, secondary] : [allowed],
                ApprovalPolicy.ReadOnly, ArtifactDirectory: artifacts.GetTaskDirectory(taskId));
            var approvals = new PolicyApprovalService((_, _, _) => throw new InvalidOperationException("Read-only preview must not prompt."));
            var executor = new ToolExecutor(new ToolRegistry([new FilePreviewTool(new AuthorizedPathResolver())]), approvals);
            var picker = new FilePreviewPickerProbe { Pick = _ => Task.FromResult<IReadOnlyList<string>>([selected]) };
            using var pane = new FilePreviewPaneViewModel(executor, new TaskArtifactContentReader(artifacts), picker);
            pane.Attach(context);
            await pane.OpenAsync(initial, token);
            Assert.True(pane.Active!.IsReady);
            Assert.Equal("original authorized content", pane.Active.Text);
            await pane.FilePicker.AddFilesCommand.ExecuteAsync(null);
            Assert.Equal(selected, pane.Active.Path);
            Assert.Equal(authorizeSecondary, pane.Active.IsReady);
            Assert.Equal(authorizeSecondary ? "synthetic secondary content" : string.Empty, pane.Active.Text);
            Assert.Equal(authorizeSecondary ? 2 : 1, Directory.GetFiles(context.ArtifactDirectory!).Length);
            if (!authorizeSecondary) Assert.Equal(MossAgent.App.Resources.InspectorText.Get("PreviewFailed"), pane.Active.Status);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
