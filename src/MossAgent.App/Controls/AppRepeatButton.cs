using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppRepeatButton : RepeatButton
{
    public AppRepeatButton() => Classes.Add("app-repeat-button");

    protected override Type StyleKeyOverride => typeof(RepeatButton);
}
