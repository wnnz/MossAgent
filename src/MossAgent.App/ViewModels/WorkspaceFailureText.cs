using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal static class WorkspaceFailureText
{
    public static string Get(TaskFailureReason? reason) =>
        reason is null or TaskFailureReason.Unknown || !Enum.IsDefined(reason.Value)
            ? WorkspaceText.Get("TaskFailureHint")
            : WorkspaceText.Get($"TaskFailure{reason}");
}
