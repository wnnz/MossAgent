using MossAgent.Domain;

namespace MossAgent.Application.Automations;

/// <summary>Terminal result reported by an automation task runner.</summary>
public sealed record AutomationRunOutcome(AutomationRunStatus Status, string? ErrorCode = null);
