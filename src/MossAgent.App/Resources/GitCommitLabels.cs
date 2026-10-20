namespace MossAgent.App.Resources;

public static class GitCommitLabels
{
    public static string Title => InspectorText.Get("GitCommitTitle");
    public static string Open => InspectorText.Get("GitCommitOpen");
    public static string Message => InspectorText.Get("GitCommitMessage");
    public static string MessageHint => InspectorText.Get("GitCommitMessageHint");
    public static string Warning => InspectorText.Get("GitCommitWarning");
    public static string Confirm => InspectorText.Get("GitCommitConfirm");
    public static string Refresh => InspectorText.Get("GitCommitRefresh");
    public static string Cancel => InspectorText.Get("GitCommitCancel");
    public static string Stop => InspectorText.Get("GitCommitStop");
}
