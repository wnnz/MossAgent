namespace MossAgent.App.ViewModels;

internal sealed class GitCommitDraftState
{
    public string Text { get; set; } = string.Empty;
    public string Persisted { get; set; } = string.Empty;
    public long Revision { get; set; }
    public bool Loaded { get; set; }
    public bool Edited { get; set; }
    public bool Conflicted { get; set; }
    public bool NeedsSaving => Text != Persisted;
}
