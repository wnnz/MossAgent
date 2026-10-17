using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace MossAgent.Browser;

internal sealed class PlaywrightBrowserTabSet
{
    private readonly IBrowserContext _context;
    private readonly bool _ownsContext;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _creation = new(1, 1);
    private readonly Dictionary<IPage, string> _ids = [];
    private readonly HashSet<IPage> _ownedPages = [];
    private bool _stopping;
    private bool _disposed;
    private Task? _disposeTask;

    public PlaywrightBrowserTabSet(IBrowserContext context, IPage initialPage, bool ownsContext, bool ownsInitialPage)
    {
        _context = context;
        _ownsContext = ownsContext;
        Register(initialPage, ownsInitialPage);
    }

    public KeyValuePair<IPage, string>[] Snapshot()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (_ownsContext)
                foreach (var page in _context.Pages.Where(page => !page.IsClosed)) Register(page, false);
            foreach (var page in _ids.Keys.Where(page => page.IsClosed).ToArray()) Remove(page);
            return _ids.ToArray();
        }
    }

    public IPage Find(string id) => Snapshot().FirstOrDefault(pair => pair.Value == id).Key
        ?? throw new InvalidOperationException($"浏览器标签不存在：{id}");

    public async Task<(IPage Page, string Id)> CreateAsync(CancellationToken cancellationToken)
    {
        await _creation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_sync) ObjectDisposedException.ThrowIf(_stopping, this);
            IPage? page = null;
            try
            {
                page = await _context.NewPageAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_stopping, this);
                    return (page, Register(page, true));
                }
            }
            catch
            {
                try { if (page is not null) await page.CloseAsync().ConfigureAwait(false); }
                finally { cancellationToken.ThrowIfCancellationRequested(); }
                throw;
            }
        }
        finally { _creation.Release(); }
    }

    public void Remove(IPage page)
    {
        lock (_sync)
        {
            if (_ids.Remove(page)) page.Popup -= HandlePopup;
            _ownedPages.Remove(page);
        }
    }

    public Task DisposeAsync()
    {
        lock (_sync)
        {
            _stopping = true;
            return _disposeTask ??= DisposeCoreAsync();
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _creation.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ownsContext) await _context.CloseAsync().ConfigureAwait(false);
            else await CloseOwnedPagesAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                _disposed = true;
                foreach (var page in _ids.Keys) page.Popup -= HandlePopup;
                _ids.Clear();
                _ownedPages.Clear();
            }
            _creation.Release();
        }
    }

    private async Task CloseOwnedPagesAsync()
    {
        Exception? failure = null;
        while (true)
        {
            IPage[] pages;
            lock (_sync)
            {
                pages = _ownedPages.ToArray();
                _ownedPages.Clear();
            }
            if (pages.Length == 0) break;
            foreach (var page in pages)
            {
                try { if (!page.IsClosed) await page.CloseAsync().ConfigureAwait(false); }
                catch (Exception error) { failure ??= error; }
            }
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private string Register(IPage page, bool owned)
    {
        if (_ids.TryGetValue(page, out var id)) return id;
        id = Guid.NewGuid().ToString("N");
        _ids.Add(page, id);
        if (owned) _ownedPages.Add(page);
        page.Popup += HandlePopup;
        return id;
    }

    private void HandlePopup(object? sender, IPage page)
    {
        lock (_sync)
        {
            if (_disposed || sender is not IPage parent || !_ids.ContainsKey(parent)) return;
            Register(page, true);
        }
    }
}
