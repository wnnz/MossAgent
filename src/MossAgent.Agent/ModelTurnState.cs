using System.Text;
using MossAgent.Application.Models;

namespace MossAgent.Agent;

internal sealed class ModelTurnState
{
    public List<ToolCallCompleted> Calls { get; } = [];
    public StringBuilder AssistantText { get; } = new();
    public bool Failed { get; set; }
    public ModelContinuation? Continuation { get; set; }
}
