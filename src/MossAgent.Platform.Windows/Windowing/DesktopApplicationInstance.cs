using System.Security.Cryptography;
using System.Text;

namespace MossAgent.Platform.Windows.Windowing;

public sealed class DesktopApplicationInstance : IDisposable
{
    private Mutex? _mutex;
    private EventWaitHandle? _signal;
    private RegisteredWaitHandle? _wait;
    private Thread? _ownerThread;
    private bool _owns;
    private bool _disposed;

    private DesktopApplicationInstance(bool isPrimary, Mutex mutex, EventWaitHandle signal, Thread? ownerThread)
    {
        IsPrimary = isPrimary;
        _mutex = mutex;
        _signal = signal;
        _ownerThread = ownerThread;
        _owns = isPrimary;
    }

    public bool IsPrimary { get; }
    public event Action? ActivationRequested;

    public static DesktopApplicationInstance Create(string dataRoot)
    {
        var name = @"Local\MossAgent." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(dataRoot))));
        var mutex = new Mutex(false, name + ".mutex");
        var acquired = false;
        try { acquired = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { acquired = true; }
        EventWaitHandle? signal = null;
        try
        {
            signal = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".activate");
            var instance = new DesktopApplicationInstance(acquired, mutex, signal, acquired ? Thread.CurrentThread : null);
            if (acquired)
                instance._wait = ThreadPool.RegisterWaitForSingleObject(signal,
                    (_, _) => instance.DispatchActivation(), null, -1, false);
            return instance;
        }
        catch
        {
            signal?.Dispose();
            if (acquired)
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            mutex.Dispose();
            throw;
        }
    }

    public void SignalActivation()
    {
        if (_disposed || IsPrimary) return;
        _signal?.Set();
    }

    private void DispatchActivation()
    {
        if (_disposed) return;
        ActivationRequested?.Invoke();
    }

    private static string Normalize(string dataRoot) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)).ToUpperInvariant();

    public void Dispose()
    {
        if (_disposed) return;
        if (_owns && !ReferenceEquals(Thread.CurrentThread, _ownerThread))
            throw new InvalidOperationException("Instance ownership must be released on the thread that acquired it.");
        _disposed = true;
        ActivationRequested = null;
        _wait?.Unregister(null);
        _wait = null;
        _signal?.Dispose();
        _signal = null;
        if (_owns)
        {
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
            _owns = false;
        }
        _ownerThread = null;
        _mutex?.Dispose();
        _mutex = null;
    }
}
