using MossAgent.Browser.Resources;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

internal sealed class BrowserTaskSessionState
{
    private readonly object _gate = new();
    private CancellationTokenSource _automation = new();
    private CancellationTokenSource? _returnProbe;
    private int _generation;
    private bool _released;
    private BrowserControlState _state = BrowserControlState.Automated;

    public BrowserControlState State
    {
        get { lock (_gate) return _state; }
    }

    public bool IsAutomated => State == BrowserControlState.Automated;

    public CancellationTokenSource EnterAutomation(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfNotAutomatedLocked();
            return CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _automation.Token);
        }
    }

    public void ThrowIfNotAutomated()
    {
        lock (_gate) ThrowIfNotAutomatedLocked();
    }

    public BrowserControlState BeginUserTakeover()
    {
        lock (_gate)
        {
            if (_state is BrowserControlState.HandedToUser or BrowserControlState.Disconnected)
            {
                return _state;
            }

            _generation++;
            _state = BrowserControlState.HandedToUser;
            CancelOperationsLocked();
            _returnProbe?.Dispose();
            _returnProbe = null;
            return _state;
        }
    }

    public bool TryBeginReturn(out int generation, out CancellationToken probeToken)
    {
        lock (_gate)
        {
            generation = _generation;
            probeToken = CancellationToken.None;
            if (_state != BrowserControlState.HandedToUser)
            {
                return false;
            }

            _state = BrowserControlState.ReturningToAutomated;
            _returnProbe = new CancellationTokenSource();
            probeToken = _returnProbe.Token;
            return true;
        }
    }

    public BrowserControlState CompleteReturn(int generation, BrowserControlState outcome)
    {
        lock (_gate)
        {
            if (generation != _generation || _state != BrowserControlState.ReturningToAutomated)
            {
                return _state;
            }

            _returnProbe?.Dispose();
            _returnProbe = null;
            _state = outcome;
            if (outcome == BrowserControlState.Automated)
            {
                _automation.Dispose();
                _automation = new CancellationTokenSource();
            }
            else
            {
                CancelOperationsLocked();
            }

            return _state;
        }
    }

    public BrowserControlState MarkDisconnected()
    {
        lock (_gate)
        {
            if (_state == BrowserControlState.Disconnected)
            {
                return _state;
            }

            _generation++;
            _state = BrowserControlState.Disconnected;
            CancelOperationsLocked();
            _returnProbe?.Dispose();
            _returnProbe = null;
            return _state;
        }
    }

    public void Release()
    {
        lock (_gate)
        {
            _released = true;
            CancelOperationsLocked();
            _automation.Dispose();
            _returnProbe?.Dispose();
        }
    }

    private void CancelOperationsLocked()
    {
        _automation.Cancel();
        _returnProbe?.Cancel();
    }

    private void ThrowIfNotAutomatedLocked()
    {
        ObjectDisposedException.ThrowIf(_released, this);
        if (_state == BrowserControlState.Automated)
        {
            return;
        }

        throw new BrowserTakeoverException(
            BrowserToolText.Get(_state == BrowserControlState.Disconnected
                ? "BrowserSessionDisconnected"
                : "BrowserTakeoverActive"),
            _state);
    }
}
