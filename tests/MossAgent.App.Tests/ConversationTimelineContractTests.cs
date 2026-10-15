using System.Xml.Linq;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTimelineContractTests
{
    [Fact]
    public void RestoredMessages_RetainTheirRecordedTimestampForPresentation()
    {
        var recordedAt = new DateTimeOffset(2026, 10, 12, 8, 30, 0, TimeSpan.Zero);
        var stored = new ConversationMessage(Guid.NewGuid(), Guid.NewGuid(), MessageRole.User, "prompt", recordedAt);
        var displayed = WorkspaceConversationMapper.ToChatMessage(stored);
        Assert.Equal(recordedAt, displayed.CreatedAt);
    }

    [Fact]
    public void Workbench_UsesAFunctionalSharedConversationTimeline()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        var view = XDocument.Load(Path.Combine(directory!.FullName, "src", "MossAgent.App", "Views", "WorkspaceChatView.axaml"));
        var timeline = Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppConversationTimeline");
        Assert.Equal("{Binding Messages}", timeline.Attribute("Messages")?.Value);
        Assert.Equal("{Binding IsRunning}", timeline.Attribute("IsRunning")?.Value);
    }
}
