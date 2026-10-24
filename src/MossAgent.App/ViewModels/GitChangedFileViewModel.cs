namespace MossAgent.App.ViewModels;

public sealed record GitChangedFileViewModel(
    string Path,
    string Status,
    bool HasStagedChange,
    bool HasUnstagedChange,
    string? OriginalPath = null)
{
    public string StatusLabel => Status switch
    {
        "??" => MossAgent.App.Resources.UiText.T("未跟踪"),
        "A " => MossAgent.App.Resources.UiText.T("已添加"),
        "D " => MossAgent.App.Resources.UiText.T("已删除"),
        " M" => MossAgent.App.Resources.UiText.T("已修改"),
        " D" => MossAgent.App.Resources.UiText.T("工作区删除"),
        "M " => MossAgent.App.Resources.UiText.T("已暂存修改"),
        "MM" => MossAgent.App.Resources.UiText.T("暂存后又修改"),
        _ when Status.Contains('R') => MossAgent.App.Resources.UiText.T("重命名"),
        _ when Status.Contains('U') => MossAgent.App.Resources.UiText.T("冲突"),
        _ => Status.Trim()
    };
}

