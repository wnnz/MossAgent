using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

internal sealed class BrowserProfileApprovalCache
{
    private BrowserProfileMode? _approvedMode;

    public bool IsApproved(BrowserProfileMode mode) => _approvedMode == mode;

    public void Approve(BrowserProfileMode mode) => _approvedMode = mode;
}
