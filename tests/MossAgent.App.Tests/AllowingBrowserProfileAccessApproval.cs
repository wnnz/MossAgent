using MossAgent.Application.Artifacts;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.App.Tests;

internal sealed class AllowingBrowserProfileAccessApproval : IBrowserProfileAccessApproval
{
    public ValueTask<bool> ConfirmAsync(
        Guid taskId, BrowserProfileMode mode, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);
}
