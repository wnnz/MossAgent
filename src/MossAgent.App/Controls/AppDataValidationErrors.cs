using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppDataValidationErrors : DataValidationErrors
{
    protected override Type StyleKeyOverride => typeof(DataValidationErrors);
}
