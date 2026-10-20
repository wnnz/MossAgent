using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCommitException(string code, string resourceKey) : Exception(ToolExecutionText.Get(resourceKey))
{
    public string Code { get; } = code;
}
