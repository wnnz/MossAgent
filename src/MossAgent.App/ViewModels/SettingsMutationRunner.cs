using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;

namespace MossAgent.App.ViewModels;

public sealed class SettingsMutationRunner : ObservableObject
{
    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }

    public Task<bool> TryExecuteAsync(Func<CancellationToken, Task> operation,
        CancellationToken ownerToken, Action<string> reportFailure, string failureKey) =>
        TryExecuteAsync(operation, ownerToken, () => reportFailure(ProviderSettingsText.Get(failureKey)));

    public async Task<bool> TryExecuteAsync(Func<CancellationToken, Task> operation,
        CancellationToken ownerToken, Action reportFailure)
    {
        if (IsRunning || ownerToken.IsCancellationRequested) return false;
        IsRunning = true;
        try
        {
            await operation(ownerToken);
            return !ownerToken.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (ownerToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            if (!ownerToken.IsCancellationRequested) reportFailure();
            return false;
        }
        finally { IsRunning = false; }
    }
}
