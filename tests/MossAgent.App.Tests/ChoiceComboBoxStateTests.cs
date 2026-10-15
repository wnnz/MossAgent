using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ChoiceComboBoxStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ChoicesReplacement_DoesNotWriteCoercionIntoSelectionAndUserCanCorrectIt(bool dark) => _session.Dispatch(() =>
    {
        var selector = new AppChoiceComboBox { Choices = ["low", "medium", "high"], SelectedChoice = "medium" };
        var window = new AppWindow
        {
            Width = 300,
            Height = 120,
            Content = selector,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var changes = 0;
            selector.PropertyChanged += (_, change) => { if (change.Property == AppChoiceComboBox.SelectedChoiceProperty) changes++; };
            selector.Choices = ["low", "high"];
            Assert.Null(selector.SelectedItem);
            Assert.Equal("medium", selector.SelectedChoice);
            selector.Choices = ["low", "high", "medium"];
            Assert.Equal("medium", selector.SelectedItem);
            Assert.Equal(0, changes);
            Assert.True(selector.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("high", selector.SelectedChoice);
            Assert.Equal(1, changes);
            selector.IsEnabled = false;
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("high", selector.SelectedChoice);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
