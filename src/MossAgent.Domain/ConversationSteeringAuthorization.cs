namespace MossAgent.Domain;

/// <summary>Immutable run-start authorization expected by each steering write transaction.</summary>
public sealed record ConversationSteeringAuthorization(ProjectProfile? Project, SourceReviewChatBinding? SourceReview);
