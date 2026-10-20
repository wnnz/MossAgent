using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed record WorkspaceSteeringAttempt(Guid RunId, ConversationDraftScope Scope, string Text, Guid Id);
