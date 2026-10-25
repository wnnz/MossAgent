using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Application.Artifacts;

public interface IBrowserProfileAccessApproval
{
    ValueTask<bool> ConfirmAsync(Guid taskId, BrowserProfileMode mode, CancellationToken cancellationToken);
}
