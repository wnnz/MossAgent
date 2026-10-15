namespace MossAgent.App.ViewModels;

public sealed record AutomationChoice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
