using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

internal static class ConversationDraftAttachmentTestData
{
    public static SqliteConversationDraftRepository Repository(SqliteFailurePersistenceFixture fixture, TimeProvider? clock = null) =>
        new(fixture.Connections, new AppDataPaths(fixture.Project.PrimaryDirectory), clock);

    public static ConversationDraftAttachment Attachment(SqliteFailurePersistenceFixture fixture, string text = "original text") =>
        new(Guid.NewGuid(), Path.Combine(fixture.Project.PrimaryDirectory, "source-never-read.txt"), "selected.txt", text,
            Encoding.UTF8.GetByteCount(text), WorkspaceAttachmentAuthorization.Create(fixture.Project));

    public static string Snapshot(SqliteFailurePersistenceFixture fixture, ConversationDraftAttachment attachment) =>
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Project.PrimaryDirectory, "draft-attachments"),
            attachment.Id.ToString("N") + ".txt", SearchOption.AllDirectories));
}
