namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitHunksException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
