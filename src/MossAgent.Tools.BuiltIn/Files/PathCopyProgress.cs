namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class PathCopyProgress
{
    public int Files { get; set; }
    public int Directories { get; set; }
    public bool HasChanges => Files > 0 || Directories > 0;
}
