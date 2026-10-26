using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record PullRequestReviewDecisionOption(PullRequestReviewDecision Decision, string Key, string Name);
