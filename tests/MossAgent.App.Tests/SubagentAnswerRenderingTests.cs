using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
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
}
