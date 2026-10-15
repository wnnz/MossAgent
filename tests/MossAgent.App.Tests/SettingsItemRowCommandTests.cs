using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsItemRowCommandTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task Actions_UseCurrentTargetAndRespectAvailabilityAndCanExecute() => _session.Dispatch(() =>
    {
        var first = new object();
        var second = new object();
        object? received = null;
        var allowed = false;
        var command = new RelayCommand<object>(target => received = target, _ => allowed);
        var row = new AppSettingsItemRow
        {
            Title = "first",
            PrimaryLabel = "启用",
            AlternatePrimaryLabel = "停用",
            SecondaryLabel = "编辑",
            ActionTarget = first,
            PrimaryCommand = command,
            SecondaryCommand = new RelayCommand<object>(target => received = target)
        };
        var window = new AppWindow { Content = row };
        window.Show();
        try
        {
            var primary = row.FindControl<AppButton>("PrimaryActionButton")!;
            var secondary = row.FindControl<AppButton>("SecondaryActionButton")!;
            Assert.False(primary.IsEffectivelyEnabled);
            PressSpace(secondary);
            Assert.Same(first, received);
            row.ActionTarget = second;
            row.Title = "second";
            row.IsPrimaryAlternate = true;
            allowed = true;
            command.NotifyCanExecuteChanged();
            Assert.True(primary.IsEffectivelyEnabled);
            Assert.Equal("停用", primary.Content);
            Assert.Equal("停用 second", AutomationProperties.GetName(primary));
            Assert.Equal("编辑 second", AutomationProperties.GetName(secondary));
            PressSpace(primary);
            Assert.Same(second, received);
            row.PrimaryCommand = null;
            Assert.False(primary.IsEffectivelyEnabled);
            row.ActionTarget = null;
            Assert.False(secondary.IsEffectivelyEnabled);
            Assert.Null(secondary.CommandParameter);
            row.SecondaryLabel = " ";
            Assert.False(secondary.IsVisible);
            row.IsPrimaryAlternate = false;
            Assert.Equal("启用", primary.Content);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
