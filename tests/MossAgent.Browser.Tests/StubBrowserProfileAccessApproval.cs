using MossAgent.Application.Artifacts;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tests;

internal sealed class StubBrowserProfileAccessApproval : IBrowserProfileAccessApproval
{
    public List<(Guid TaskId, BrowserProfileMode Mode)> Calls { get; } = [];
    public Func<BrowserProfileMode, bool> Decision { get; set; } = _ => true;

    public ValueTask<bool> ConfirmAsync(
        Guid taskId,
        BrowserProfileMode mode,
        CancellationToken cancellationToken)
    {
        Calls.Add((taskId, mode));
        return ValueTask.FromResult(Decision(mode));
    }
}
