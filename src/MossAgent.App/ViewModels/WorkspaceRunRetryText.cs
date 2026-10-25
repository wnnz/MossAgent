using MossAgent.Application.Agent;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceRunRetryText
{
    public static string Confirmation(RunFailureProgress progress) => WorkspaceText.Get(progress switch
    {
        RunFailureProgress.PossiblySubmitted => "RunRetryConfirmPossiblySubmitted",
        RunFailureProgress.ToolsExecuted => "RunRetryConfirmToolsExecuted",
        _ => "RunRetryConfirmUnknown"
    });
}
