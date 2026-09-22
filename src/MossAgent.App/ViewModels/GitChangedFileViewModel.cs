namespace MossAgent.App.ViewModels;

public sealed record GitChangedFileViewModel(
    string Path,
    string Status,
    bool HasStagedChange,
    bool HasUnstagedChange)
{
    public string StatusLabel => Status switch
    {
        "??" => "未跟踪",
        "A " => "已添加",
        "D " => "已删除",
        " M" => "已修改",
        " D" => "工作区删除",
        "M " => "已暂存修改",
        "MM" => "暂存后又修改",
        _ when Status.Contains('R') => "重命名",
        _ when Status.Contains('U') => "冲突",
        _ => Status.Trim()
    };
}
