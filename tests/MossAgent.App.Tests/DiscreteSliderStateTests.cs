using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DiscreteSliderStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Reattachment_RestoresInputWithoutCommittingDetachedChanges(bool dark) => _session.Dispatch(() =>
    {
        var component = new AppDiscreteSlider { StepValues = ["low", "medium", "high"], SelectedStep = "medium" };
        var slider = component.FindControl<AppSlider>("StepSlider")!;
        var window = new AppWindow
        {
            Width = 280,
            Height = 120,
            Content = component,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            var changes = 0;
            component.PropertyChanged += (_, change) => { if (change.Property == AppDiscreteSlider.SelectedStepProperty) changes++; };
            for (var cycle = 0; cycle < 2; cycle++)
            {
                window.Content = null;
                slider.Value = 0;
                Assert.Equal("medium", component.SelectedStep);
                window.Content = component;
                window.UpdateLayout();
                Assert.Equal(1, slider.Value);
            }
            Assert.Equal(0, changes);
            Assert.True(slider.Focus());
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.End, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("high", component.SelectedStep);
            Assert.Equal(1, changes);
            component.IsEnabled = false;
            window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Home, RawInputModifiers.None, PhysicalKey.None, null);
            Assert.Equal("high", component.SelectedStep);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
