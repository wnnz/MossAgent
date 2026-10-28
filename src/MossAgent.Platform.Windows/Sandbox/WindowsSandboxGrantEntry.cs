using System.Security.AccessControl;
using System.Security.Principal;

namespace MossAgent.Platform.Windows.Sandbox;

/// <summary>
/// One DACL grant for a (sid, path) pair. All state changes run under <see cref="Sync"/>;
/// the entry only removes an ACE it added itself, never a pre-existing matching rule.
/// </summary>
internal sealed class WindowsSandboxGrantEntry(string sid, string path, bool writable, SecurityIdentifier identity)
{
    public object Sync { get; } = new();
    public string Sid { get; } = sid;
    public string Path { get; } = path;
    public bool Writable { get; } = writable;
    public int References { get; private set; }
    public DateTimeOffset IdleSince { get; private set; } = DateTimeOffset.MaxValue;
    private bool _applied;
    private bool _removed;
    private FileSystemAccessRule? _addedRule;

    private FileSystemRights Needed => Writable ? FileSystemRights.Modify : FileSystemRights.ReadAndExecute;

    public void Acquire()
    {
        lock (Sync)
        {
            References++;
            IdleSince = DateTimeOffset.MaxValue;
            if (_applied && !_removed) return;
            Apply();
        }
    }

    public void Release(bool immediate)
    {
        lock (Sync)
        {
            if (References > 0) References--;
            if (References > 0 || Removed) return;
            if (immediate) RemoveNow();
            else IdleSince = DateTimeOffset.UtcNow;
        }
    }

    public bool SweepIdle(DateTimeOffset now, TimeSpan threshold)
    {
        lock (Sync)
        {
            if (References > 0) return false;
            if (Removed) return true;
            if (now - IdleSince < threshold) return false;
            RemoveNow();
            return true;
        }
    }

    public void RemoveBestEffort()
    {
        lock (Sync) RemoveNow();
    }

    public bool IsRemoved => _removed;
    private bool Removed => _removed;

    private void Apply()
    {
        try
        {
            var isDirectory = Directory.Exists(Path);
            if (!isDirectory && !File.Exists(Path))
                throw new FileNotFoundException("授权目标不存在。", Path);
            var security = isDirectory
                ? (FileSystemSecurity)new DirectoryInfo(Path).GetAccessControl()
                : new FileInfo(Path).GetAccessControl();
            var rule = CreateRule(isDirectory);
            _addedRule = HasMatchingRule(security) ? null : rule;
            if (_addedRule is not null)
            {
                security.AddAccessRule(_addedRule);
                WriteBack(security, isDirectory);
            }
            _applied = true;
            _removed = false;
        }
        catch (Exception failure) when (failure is not InvalidOperationException)
        {
            throw new InvalidOperationException($"沙箱授权失败：{Path}", failure);
        }
    }

    private FileSystemAccessRule CreateRule(bool isDirectory) => new(identity, Needed,
        isDirectory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None,
        PropagationFlags.None, AccessControlType.Allow);

    private bool HasMatchingRule(FileSystemSecurity security)
    {
        var found = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Any(rule => rule.IdentityReference.Equals(identity)
                && rule.AccessControlType == AccessControlType.Allow
                && (rule.FileSystemRights & Needed) == Needed);
        return found;
    }

    private void WriteBack(FileSystemSecurity security, bool isDirectory)
    {
        if (isDirectory) new DirectoryInfo(Path).SetAccessControl((DirectorySecurity)security);
        else new FileInfo(Path).SetAccessControl((FileSecurity)security);
    }

    private void RemoveNow()
    {
        if (_removed) return;
        if (_addedRule is null) { _removed = true; return; }
        try
        {
            var isDirectory = Directory.Exists(Path);
            var security = isDirectory
                ? (FileSystemSecurity)new DirectoryInfo(Path).GetAccessControl()
                : File.Exists(Path) ? new FileInfo(Path).GetAccessControl() : null;
            if (security is not null)
            {
                security.RemoveAccessRuleSpecific(_addedRule);
                WriteBack(security, isDirectory);
            }
            // Either the ACE is gone or the path no longer exists; nothing left to clean.
            _removed = true;
        }
        catch (Exception) { /* keep the entry so the next sweep or Dispose retries */ }
    }
}
