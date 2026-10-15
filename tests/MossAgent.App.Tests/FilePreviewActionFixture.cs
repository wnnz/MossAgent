using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.Tests;

internal sealed class FilePreviewActionFixture : IDisposable
{
    public FilePreviewActionFixture(ApprovalPolicy policy = ApprovalPolicy.FullAccess)
    {
        Context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], policy);
        Pane = new FilePreviewPaneViewModel(new InspectorProbeToolExecutor { Execute = (request, context, token) =>
        {
            Calls.Add((request, context));
            return request.ToolName == "filesystem.reveal_file" ? Reveal(request, context, token)
                : Task.FromResult(new ToolResult(true, "preview", Artifacts: [new("file", "snapshot", "text/plain", 0, "hash")]));
        } }, new PreviewProbeArtifactReader());
        Pane.Attach(Context);
    }

    public FilePreviewPaneViewModel Pane { get; }
    public ToolExecutionContext Context { get; }
    public List<(ToolRequest Request, ToolExecutionContext Context)> Calls { get; } = [];
    public Func<ToolRequest, ToolExecutionContext, CancellationToken, Task<ToolResult>> Reveal { get; set; } =
        (_, _, _) => Task.FromResult(ToolResult.Success("revealed", string.Empty));
    public Task OpenAsync(string path = "..notes.cs") => Pane.OpenAsync(path, Xunit.TestContext.Current.CancellationToken);
    public void Dispose() => Pane.Dispose();
}
