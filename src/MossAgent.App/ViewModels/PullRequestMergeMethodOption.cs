using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record PullRequestMergeMethodOption(PullRequestMergeMethod Method, string Key, string Name);
