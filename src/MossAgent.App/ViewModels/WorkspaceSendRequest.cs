using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed record WorkspaceSendRequest(
    string Draft,
    ConversationDraftScope Scope,
    ProjectProfile? Project,
    AgentTask? ExistingTask,
    bool UseWorktree,
    ConversationModelPreference? Preference,
    AiProvider Provider,
    ModelProfile Model,
    ApprovalPolicy ApprovalPolicy,
    string? AgentInstructions,
    WorkspaceAttachmentViewModel[] Attachments,
    ChatMessageViewModel[] VisibleMessages,
    string? WorktreeBaseReference = null)
{
    internal static WorkspaceSendRequest Capture(WorkspaceViewModel workspace) => new(
        workspace.ComposerText, workspace.Drafts.Scope, workspace.Catalog.SelectedProject, workspace.Catalog.SelectedTask,
        workspace.Catalog.Environment.UseWorktree, workspace.ConversationPreferences?.Capture(), workspace.SelectedProvider!,
        workspace.SelectedModel! with { ReasoningEffort = workspace.ModelPicker.EffectiveReasoningEffort }, workspace.ApprovalPolicy,
        workspace.SelectedAgent?.SystemPrompt, workspace.Attachments.Items.ToArray(),
        workspace.Catalog.SelectedTask is null ? [] : workspace.Messages.ToArray(),
        workspace.Catalog.Environment.UseWorktree && !string.IsNullOrWhiteSpace(workspace.Catalog.Environment.BaseReference)
            ? workspace.Catalog.Environment.BaseReference.Trim() : null);
}
