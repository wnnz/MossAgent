using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SubagentAnswerRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, "Light")]
    [InlineData(740, "Light")]
    [InlineData(280, "Dark")]
    [InlineData(740, "Dark")]
    public Task Answer_RendersStructuredMarkdownWhileToolOutputRemainsLiteral(double width, string theme) =>
        _session.Dispatch(() =>
        {
            const string answer = "# Review\n\n**Found** `Count`.\n\n- Keep the value.\n\n"
                + "```csharp\npublic const int Count = 42;\n```\n\n"
                + "| Field | Value |\n| --- | --- |\n| Count | 42 |";
            const string toolOutput = "# Literal output\n```not-a-code-block\n**retain every marker**";
            var payload = new ToolConversationPayload("subagent.run", true, "Done", null,
                [new ToolDialogueMessage("子代理", answer),
                 new ToolDialogueMessage("工具 · filesystem.read_file", toolOutput)]);
            var viewer = new AppScrollViewer
            {
                Content = new SubagentDialogueView { DataContext = ChatMessageViewModel.CreateTool(payload) }
            };
            viewer.Classes.Add("tool-details-scroll");
            var window = new Window
            {
                Width = width,
                Height = 600,
                Content = viewer,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var descendants = viewer.GetVisualDescendants().ToArray();
                var code = Assert.Single(descendants.OfType<SyntaxHighlightedSelectableTextBlock>());
                Assert.Equal("public const int Count = 42;", code.Code);
                Assert.Equal("csharp", code.Language);
                Assert.Contains(descendants.OfType<MarkdownSelectableTextBlock>(), text => text.Markdown == "Review");
                Assert.Contains(descendants.OfType<MarkdownSelectableTextBlock>(), text => text.Markdown == "**Found** `Count`.");
                Assert.Contains(descendants.OfType<MarkdownSelectableTextBlock>(), text => text.Markdown == "Count");
                Assert.Contains(descendants.OfType<AppSelectableText>(), text => text.Text == toolOutput);
                Assert.InRange(viewer.Extent.Width, 0, viewer.Viewport.Width + 1);
                Assert.Equal(answer, payload.Dialogue![0].Content);
                Assert.Equal(toolOutput, payload.Dialogue[1].Content);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task CompletedAndCancelledRunMetadataIsVisibleInToolHistory(string theme) =>
        _session.Dispatch(() =>
        {
            var run = CreateRun(SubagentRunStatus.Cancelled);
            var payload = new ToolConversationPayload("subagent.run", false,
                "父任务已取消，子代理停止等待；不自动重试。", null,
                [new ToolDialogueMessage("状态", "已取消")], "tool_cancelled",
                SubagentRun: run);
            var message = ChatMessageViewModel.CreateTool(payload);
            message.IsToolExpanded = true;
            var view = new WorkspaceMessageView { DataContext = message };
            var window = new Window
            {
                Width = 740,
                Height = 500,
                Content = view,
                RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Contains(view.GetVisualDescendants().OfType<AppText>(),
                    text => text.Text == payload.SubagentRunIdentity);
                Assert.Contains("已取消", payload.SubagentRunIdentity);
                Assert.True(payload.HasSubagentRun);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static SubagentRun CreateRun(SubagentRunStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "call",
            Guid.NewGuid(), "review", "trigger", "prompt", Guid.NewGuid(), "provider",
            ProviderProtocol.OpenAiResponses, Guid.NewGuid(), "model", 128000, 4096,
            "high", true, Guid.NewGuid().ToString("N"), status,
            "cancelled", "tool_cancelled", now, now, now);
    }
}
