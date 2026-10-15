using MossAgent.Application.Automations;

namespace MossAgent.Infrastructure.Persistence;

public sealed class AutomationSchedulerLease(AppDataPaths paths) : IAutomationLease, IDisposable
{
    private FileStream? _stream;

    public bool TryAcquire()
    {
        if (_stream is not null) return true;
        try
        {
            _stream = new FileStream(Path.Combine(paths.Root, "automations.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException) { return false; }
    }

    public void Release()
    {
        _stream?.Dispose();
        _stream = null;
    }

    public void Dispose() => Release();
}
