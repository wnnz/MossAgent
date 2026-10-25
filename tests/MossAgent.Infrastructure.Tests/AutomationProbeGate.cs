using MossAgent.Application.Automations;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Tests;

internal sealed class AutomationProbeGate : IAutomationAuthorizationGate
{
    public string? BlockCode { get; set; }
    public List<AutomationSchedule> Evaluations { get; } = [];

    public Task<AutomationAuthorizationDecision> EvaluateAsync(AutomationSchedule schedule, CancellationToken cancellationToken)
    {
        Evaluations.Add(schedule);
        return Task.FromResult(BlockCode is null
            ? new AutomationAuthorizationDecision(new AutomationRunContext(schedule,
                new ProjectProfile(schedule.ProjectId, "probe", "/probe", ["/probe"], DateTimeOffset.UtcNow),
                new AiProvider(schedule.ProviderId, "probe", ProviderProtocol.OpenAiResponses,
                    new Uri("http://localhost"), "key", null, false, true, new Dictionary<string, string>()),
                new ModelProfile(schedule.ModelId, schedule.ProviderId, "probe-model", "probe", 32000,
                    1024, "medium", null, null, false, false, false, true, "{}")), null)
            : new AutomationAuthorizationDecision(null, BlockCode));
    }
}
