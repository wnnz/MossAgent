using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Tools.Abstractions.Terminal;

namespace MossAgent.App.ViewModels;

public sealed class TerminalPaneViewModel : ObservableObject
{
    private readonly ITerminalSessionManager _terminals;
    private Guid? _taskId;
    private string? _workingDirectory;
    private string _output = string.Empty;
    private string _input = string.Empty;

    public TerminalPaneViewModel(ITerminalSessionManager terminals)
    {
        _terminals = terminals;
        _terminals.OutputReceived += OnOutputReceived;
        StartCommand = new AsyncRelayCommand(StartAsync, HasTask);
        SendCommand = new AsyncRelayCommand(SendAsync, HasTask);
        StopCommand = new AsyncRelayCommand(StopAsync, HasTask);
    }

    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand SendCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public string Output { get => _output; private set => SetProperty(ref _output, value); }
    public string Input { get => _input; set => SetProperty(ref _input, value); }

    public void AttachTask(Guid taskId, string workingDirectory)
    {
        _taskId = taskId;
        _workingDirectory = workingDirectory;
        Output = string.Empty;
        NotifyCommands();
    }

    private bool HasTask() => _taskId is not null;

    private async Task StartAsync()
    {
        await _terminals.StartAsync(
            _taskId!.Value, _workingDirectory!, CancellationToken.None);
    }

    private async Task SendAsync()
    {
        if (string.IsNullOrEmpty(Input))
        {
            return;
        }

        var value = Input.EndsWith('\r') || Input.EndsWith('\n') ? Input : Input + "\r";
        Input = string.Empty;
        await _terminals.WriteAsync(_taskId!.Value, value, CancellationToken.None);
    }

    private async Task StopAsync() => await _terminals.StopAsync(_taskId!.Value);

    private void OnOutputReceived(object? sender, TerminalOutputEventArgs eventArgs)
    {
        if (eventArgs.TaskId != _taskId)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var combined = Output + eventArgs.Output;
            Output = combined.Length <= 200_000 ? combined : combined[^200_000..];
        });
    }

    private void NotifyCommands()
    {
        StartCommand.NotifyCanExecuteChanged();
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }
}

