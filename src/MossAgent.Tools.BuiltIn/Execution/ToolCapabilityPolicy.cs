using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Execution;

public static class ToolCapabilityPolicy
{
    private const ToolCapability KnownCapabilities =
        ToolCapability.FileRead
        | ToolCapability.FileWrite
        | ToolCapability.Process
        | ToolCapability.Network
        | ToolCapability.BrowserRead
        | ToolCapability.BrowserWrite
        | ToolCapability.ScreenCapture;

    private const ToolCapability ReadOnlyDeniedCapabilities =
        ToolCapability.FileWrite
        | ToolCapability.BrowserWrite
        | ToolCapability.ScreenCapture;

    private const ToolCapability InteractiveCapabilities =
        ToolCapability.FileWrite
        | ToolCapability.BrowserRead
        | ToolCapability.BrowserWrite
        | ToolCapability.ScreenCapture;

    public static bool IsAllowed(ToolDescriptor descriptor, ApprovalPolicy approvalPolicy)
    {
        if ((descriptor.RequiredCapabilities & ~KnownCapabilities) != ToolCapability.None)
        {
            return false;
        }

        return approvalPolicy != ApprovalPolicy.ReadOnly
            || descriptor.RiskLevel == ToolRiskLevel.ReadOnly
            && (descriptor.RequiredCapabilities & ReadOnlyDeniedCapabilities) == ToolCapability.None;
    }

    public static bool RequiresInteractiveApproval(ToolDescriptor descriptor) =>
        (descriptor.RequiredCapabilities & InteractiveCapabilities) != ToolCapability.None;
}
