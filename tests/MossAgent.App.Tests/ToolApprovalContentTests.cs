using System.Text;
using System.Text.Json;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ToolApprovalContentTests
{
    [Theory]
    [InlineData("filesystem.write_file", "content")]
    [InlineData("shell.run", "command")]
    [InlineData("terminal.write", "text")]
    [InlineData("browser.type", "text")]
    public void KnownBodies_RequireExplicitRevealAndClearWhenHidden(string tool, string field)
    {
        var preview = Preview(tool, new Dictionary<string, object>
        {
            [field] = "body marker", ["apiKey"] = "credential marker", ["url"] = "private url marker"
        });

        Assert.True(preview.IsAvailable);
        Assert.False(preview.IsOpen);
        Assert.Equal(string.Empty, preview.PageText);
        Assert.Equal(string.Empty, preview.RangeLabel);
        Assert.False(preview.NextCommand.CanExecute(null));
        preview.ToggleCommand.Execute(null);
        Assert.True(preview.IsOpen);
        Assert.Contains("body marker", preview.PageText, StringComparison.Ordinal);
        Assert.DoesNotContain("credential marker", preview.PageText, StringComparison.Ordinal);
        Assert.DoesNotContain("private url marker", preview.PageText, StringComparison.Ordinal);
        preview.ToggleCommand.Execute(null);
        Assert.False(preview.IsOpen);
        Assert.Equal(string.Empty, preview.PageText);
        Assert.Equal(string.Empty, preview.RangeLabel);
        Assert.False(preview.HasMultiplePages);
    }

    [Theory]
    [InlineData("mcp.example")]
    [InlineData("browser.navigate")]
    [InlineData("unknown")]
    public void UnknownOrSensitiveArgumentTypes_DoNotExposeContent(string tool)
    {
        var preview = Preview(tool, new { content = "private marker", text = "private marker", command = "private marker" });

        Assert.False(preview.IsAvailable);
        Assert.False(preview.ToggleCommand.CanExecute(null));
        preview.ToggleCommand.Execute(null);
        Assert.Equal(string.Empty, preview.PageText);
    }

    [Fact]
    public void NonStringAndMissingBodies_AreUnavailable()
    {
        foreach (var arguments in new[] { "null", "[]", "{}", "{\"content\":null}", "{\"content\":42}" })
        {
            using var document = JsonDocument.Parse(arguments);
            var preview = new ToolApprovalContentViewModel("filesystem.write_file", document.RootElement);
            Assert.False(preview.IsAvailable);
        }
    }

    [Fact]
    public void ReplacementPreview_DistinguishesOldNewAndEmptyText()
    {
        var preview = Preview("filesystem.replace_text", new { oldText = "old body", newText = string.Empty });
        preview.ToggleCommand.Execute(null);

        Assert.Equal(ApprovalText.Get("OldTextBody") + Environment.NewLine + "old body"
            + Environment.NewLine + Environment.NewLine + ApprovalText.Get("NewTextBody")
            + Environment.NewLine + ApprovalText.Get("EmptyContent"), preview.PageText);
    }

    [Fact]
    public void Pagination_PreservesEveryCharacterAndSurrogatePair()
    {
        var prefix = ApprovalText.Get("ContentBody") + Environment.NewLine;
        var body = new string('a', 4095 - prefix.Length) + "😀" + new string('中', 9000) + "end";
        var preview = Preview("filesystem.write_file", new { content = body });
        preview.ToggleCommand.Execute(null);
        var firstPage = preview.PageText;
        var output = new StringBuilder();

        while (true)
        {
            var page = preview.PageText;
            Assert.InRange(page.Length, 1, 4096);
            Assert.False(char.IsHighSurrogate(page[^1]));
            Assert.False(char.IsLowSurrogate(page[0]));
            output.Append(page);
            if (!preview.NextCommand.CanExecute(null)) break;
            preview.NextCommand.Execute(null);
        }
        Assert.Equal(prefix + body, output.ToString());
        Assert.True(preview.HasMultiplePages);
        while (preview.PreviousCommand.CanExecute(null)) preview.PreviousCommand.Execute(null);
        Assert.Equal(firstPage, preview.PageText);
        preview.Close();
        Assert.False(preview.PreviousCommand.CanExecute(null));
        preview.ToggleCommand.Execute(null);
        Assert.Equal(firstPage, preview.PageText);
    }

    [Fact]
    public void ControlAndDirectionCharacters_AreEscapedButWhitespaceIsPreserved()
    {
        var preview = Preview("filesystem.write_file", new { content = "before\u0000\u001b\u202e\u2028\r\n\tafter" });
        preview.ToggleCommand.Execute(null);

        Assert.Contains("before\\u0000\\u001B\\u202E\\u2028\r\n\tafter", preview.PageText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueueAdvancement_ClosesPreviousBodyAndDoesNotRevealTheNext(bool cancelFirst)
    {
        var approvals = new ApprovalViewModel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var descriptor = new ToolDescriptor("filesystem.write_file", "fixture", "{}",
            ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var first = approvals.RequestAsync(descriptor, new ToolRequest("first", descriptor.Name,
            JsonSerializer.SerializeToElement(new { content = "first body" })), cancellation.Token).AsTask();
        var previousPreview = Assert.IsType<ToolApprovalContentViewModel>(approvals.ContentPreview);
        previousPreview.ToggleCommand.Execute(null);
        var next = approvals.RequestAsync(descriptor, new ToolRequest("next", descriptor.Name,
            JsonSerializer.SerializeToElement(new { content = "next body" })), TestContext.Current.CancellationToken);
        Assert.True(previousPreview.IsOpen);
        if (cancelFirst)
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        }
        else
        {
            approvals.ApproveCommand.Execute(approvals.CurrentRequestId);
            Assert.True(await first);
        }
        Assert.False(previousPreview.IsOpen);
        Assert.Equal(string.Empty, previousPreview.PageText);
        var nextPreview = Assert.IsType<ToolApprovalContentViewModel>(approvals.ContentPreview);
        Assert.NotSame(previousPreview, nextPreview);
        Assert.False(nextPreview.IsOpen);
        nextPreview.ToggleCommand.Execute(null);
        Assert.DoesNotContain("first body", nextPreview.PageText, StringComparison.Ordinal);
        approvals.DenyCommand.Execute(approvals.CurrentRequestId);
        Assert.False(await next);
        Assert.Null(approvals.ContentPreview);
        Assert.Equal(string.Empty, nextPreview.PageText);
    }

    private static ToolApprovalContentViewModel Preview(string tool, object arguments) =>
        new(tool, JsonSerializer.SerializeToElement(arguments));
}
