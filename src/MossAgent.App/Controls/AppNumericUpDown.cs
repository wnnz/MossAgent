using Avalonia.Controls;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed class AppNumericUpDown : NumericUpDown
{
    public string IncreaseLabel => SettingsText.Get("NumericIncrease");
    public string DecreaseLabel => SettingsText.Get("NumericDecrease");

    public AppNumericUpDown()
    {
        Classes.Add("app-numeric-up-down");
    }

    protected override Type StyleKeyOverride => typeof(NumericUpDown);
}
