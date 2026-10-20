using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal static class ConversationFollowupTestData
{
    public static ConversationFollowup Create(AgentTask task, string prompt = "queued input") =>
        new(Guid.NewGuid(), task.Id, task.ProjectId, prompt,
            new(Guid.NewGuid(), Guid.NewGuid(), "selected-model", "Selected model", 32768, 2048,
                "custom-thought", 0.25, 0.8, false, true, false, true, "{\"seed\":123}"),
            Guid.NewGuid(), "saved agent instructions", task.ProjectId is null ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime,
            task.ProjectId is null ? [] : ["D:\\authorized"], task.WorktreePath, [], DateTimeOffset.UtcNow);
}
