namespace MossAgent.Tools.Abstractions;

public sealed record ToolDescriptor(
    string Name,
    string Description,
    string InputSchemaJson,
    ToolRiskLevel RiskLevel,
    ToolCapability RequiredCapabilities);

