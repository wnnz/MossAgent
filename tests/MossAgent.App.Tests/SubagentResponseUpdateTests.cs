using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SubagentResponseUpdateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ProgressCompletionAndRestore_UseTheSameResponseRenderer() =>
        _session.Dispatch(() =>
        {
            const string answer = "# Complete\n\n```csharp\nint result = 42;\n```";
            var partial = new ToolConversationPayload("subagent.run", true, "Running", null,
                [new ToolDialogueMessage("子代理", "**Partial**", true)]);
            var message = ChatMessageViewModel.CreateTool(partial, "call-update", true);
            var view = new SubagentDialogueView();
            view.DataContext = message;
            var window = new Window { Width = 420, Height = 500, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Contains(view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
                    text => text.Markdown == "**Partial**");
                var completed = partial with { Dialogue = [new ToolDialogueMessage("子代理", answer)] };
                message.UpdateTool(completed, false);
                window.UpdateLayout();
                Assert.False(message.IsToolRunning);
                Assert.Equal("int result = 42;", Assert.Single(view.GetVisualDescendants()
                    .OfType<SyntaxHighlightedSelectableTextBlock>()).Code);
                Assert.DoesNotContain(view.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
                    text => text.Markdown == "**Partial**");
                var serialized = JsonSerializer.Serialize(completed);
                Assert.DoesNotContain("\"Blocks\"", serialized);
                Assert.True(WorkspaceToolMessageSerializer.TryDeserialize(serialized, out var restored));
                Assert.Equal(answer, Assert.Single(restored!.Dialogue!).Content);
                message.UpdateTool(restored, false);
                window.UpdateLayout();
                Assert.Equal("int result = 42;", Assert.Single(view.GetVisualDescendants()
                    .OfType<SyntaxHighlightedSelectableTextBlock>()).Code);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
}
