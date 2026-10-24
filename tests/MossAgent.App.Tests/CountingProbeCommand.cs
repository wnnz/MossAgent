using System.Windows.Input;

namespace MossAgent.App.Tests;

internal sealed class CountingProbeCommand : ICommand
{
    public int Calls { get; private set; }
    public bool Enabled { get; set; } = true;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => Enabled;
    public void Execute(object? parameter) => Calls++;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
