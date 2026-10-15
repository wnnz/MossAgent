using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SharedStyleClassStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task VariantAndStateChanges_PreserveSharedFieldStyle(bool dark) => _session.Dispatch(() =>
    {
        var input = new AppInput { Text = "草稿", Classes = { "field-multiline" } };
        var checkbox = new AppCheckBox { Content = "启用", IsChecked = true };
        var window = new AppWindow
        {
            Width = 640,
            Height = 240,
            Content = new StackPanel { Children = { input, checkbox } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(72, input.MinHeight);
            input.Classes.Remove("field-multiline");
            input.Classes.Set("selected", true);
            checkbox.IsChecked = false;
            window.UpdateLayout();
            Assert.Equal(32, input.MinHeight);
            Assert.Equal("草稿", input.Text);
            Assert.Contains("app-input", input.Classes);
            Assert.Contains("selected", input.Classes);
            Assert.Contains("app-check-box", checkbox.Classes);
            Assert.NotNull(checkbox.Theme);
            Assert.Equal(28, checkbox.MinHeight);
            Assert.False(checkbox.IsChecked);
            input.Classes.Set("selected", false);
            Assert.DoesNotContain("selected", input.Classes);
            Assert.Contains("app-input", input.Classes);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
