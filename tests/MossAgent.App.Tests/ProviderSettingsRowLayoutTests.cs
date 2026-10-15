using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderSettingsRowLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320, true)]
    [InlineData(true, 320, true)]
    [InlineData(false, 640, false)]
    [InlineData(true, 640, false)]
    public Task ProviderHeader_ReservesBadgesCountAndActions(bool dark, int width, bool enabled) => _session.Dispatch(() =>
    {
        var provider = ProviderSettingsRowTestData.Provider(enabled);
        var header = new AppProviderSettingsHeader
        {
            Provider = provider,
            ModelCount = 12,
            EditCommand = new RelayCommand(() => { }),
            ToggleCommand = new RelayCommand(() => { }),
            ToggleCommandParameter = provider
        };
        var window = Window(header, dark, width);
        try
        {
            var name = header.FindControl<AppText>("ProviderNameText")!;
            var endpoint = header.FindControl<AppText>("EndpointText")!;
            var count = header.FindControl<AppText>("ModelCountText")!;
            var edit = header.FindControl<AppButton>("EditProviderButton")!;
            var toggle = header.FindControl<AppIconButton>("ToggleModelsButton")!;
            Assert.Equal(provider.Name, name.Text);
            Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
            Assert.Equal(TextTrimming.CharacterEllipsis, endpoint.TextTrimming);
            Assert.Equal("模型 12", count.Text);
            Assert.True(header.FindControl<AppText>("DefaultBadge")!.IsVisible);
            Assert.Equal(!enabled, header.FindControl<AppText>("DisabledBadge")!.IsVisible);
            Before(name, edit, header);
            Before(endpoint, count, header);
            Before(count, edit, header);
            Assert.InRange(Math.Abs(Center(toggle, header) - Center(edit, header)), 0, 0.5);
            Assert.InRange(Origin(edit, header).X + edit.Bounds.Width, 0, header.Bounds.Width);
            Assert.True(edit.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 320)]
    [InlineData(true, 320)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task ModelRow_KeepsLongLabelsBeforeCenteredActions(bool dark, int width) => _session.Dispatch(() =>
    {
        var model = ProviderSettingsRowTestData.Model(Guid.NewGuid()) with { ReasoningEffort = new string('x', 120) };
        var row = new AppModelSettingsRow
        {
            Model = model,
            EditCommand = new RelayCommand(() => { }),
            DeleteCommand = new RelayCommand(() => { })
        };
        var window = Window(row, dark, width);
        try
        {
            var name = row.FindControl<AppText>("ModelNameText")!;
            var identity = row.FindControl<AppText>("ModelIdText")!;
            var context = row.FindControl<AppText>("ContextText")!;
            var reasoning = row.FindControl<AppText>("ReasoningText")!;
            var edit = row.FindControl<AppButton>("EditModelButton")!;
            var delete = row.FindControl<AppButton>("DeleteModelButton")!;
            Assert.Equal(model.DisplayName, name.Text);
            Assert.Equal(model.ModelId, identity.Text);
            foreach (var text in new[] { name, identity, context, reasoning })
            {
                Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
                Before(text, edit, row);
            }
            Before(name, identity, row);
            Before(context, reasoning, row);
            Assert.InRange(Math.Abs(Center(name, row) - Center(identity, row)), 0, 0.5);
            Assert.InRange(Math.Abs(Center(edit, row) - Center(delete, row)), 0, 0.5);
            Assert.InRange(Origin(delete, row).X + delete.Bounds.Width, 0, row.Bounds.Width);
            Assert.Equal(model.ModelId, ToolTip.GetTip(identity));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Window(Control content, bool dark, int width)
    {
        var window = new AppWindow
        {
            Width = width,
            Height = 180,
            Content = content,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Point Origin(Control control, Control relative) => control.TranslatePoint(new Point(), relative)!.Value;
    private static double Center(Control control, Control relative) => Origin(control, relative).Y + control.Bounds.Height / 2;

    private static void Before(Control label, Control action, Control relative)
    {
        Assert.True(label.Bounds.Width > 0);
        Assert.InRange(Origin(label, relative).X + label.Bounds.Width, 0, Origin(action, relative).X);
    }
}
