namespace MossAgent.Tools.Abstractions.Git;

public sealed record GitRemotesUpstream(string? Branch, string? Remote, string? Merge, string? PushRemote);
