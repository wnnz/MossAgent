using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitPushException(string code, string resource) : Exception(ToolExecutionText.Get(resource))
{
    public string Code { get; } = code;
}
