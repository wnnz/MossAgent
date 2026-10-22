using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class CompactSurfaceRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320, 1)]
    [InlineData(true, 320, 1)]
    [InlineData(false, 360, 1)]
    [InlineData(true, 360, 1)]
    [InlineData(false, 460, 1)]
    [InlineData(true, 460, 1)]
    [InlineData(false, 800, 1)]
    [InlineData(true, 800, 1)]
    [InlineData(false, 360, 1.25)]
    [InlineData(true, 360, 1.25)]
    [InlineData(false, 800, 2)]
    [InlineData(true, 800, 2)]
    public Task LongModelNames_KeepComposerActionsVisible(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var view = new WorkspaceComposerView { DataContext = ComposerContext(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom };
        var window = Window(view, dark, width, 280);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            Save(window, FormattableString.Invariant($"composer-{dark}-{width}-{scale}x"));
            var surface = Assert.Single(view.GetVisualDescendants().OfType<AppCard>(), card => card.Classes.Contains("composer-surface"));
            var model = Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(), button => button.Classes.Contains("composer-model-picker"));
            var send = view.FindControl<AppIconButton>("SendButton")!;
            var policy = Assert.Single(view.GetVisualDescendants().OfType<AppApprovalPolicyButton>());
            Assert.Equal("OpenAI / GPT-5.4 Pro — extended context development model", model.Label);
            AssertContained(model, surface);
            AssertContained(send, surface);
            AssertContained(policy, surface);
            var modelLabel = Assert.Single(model.GetVisualDescendants().OfType<AppText>(), text => text.Text == model.Label);
            Assert.True(modelLabel.Bounds.Width >= 120, $"Model name has no readable space: {modelLabel.Bounds}");
            Assert.True(Right(model, surface) <= Origin(send, surface).X);
            Assert.Equal(new Size(28, 28), send.Bounds.Size);
            Assert.Equal(ApprovalPolicy.AskEveryTime, policy.Policy);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 320, 1)]
    [InlineData(true, 320, 1)]
    [InlineData(false, 640, 1)]
    [InlineData(true, 640, 1)]
    [InlineData(false, 320, 1.25)]
    [InlineData(true, 320, 1.25)]
    [InlineData(false, 640, 2)]
    [InlineData(true, 640, 2)]
    public Task SettingsRows_KeepTitlesReadableAlongsideMetadata(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var row = new AppSettingsItemRow
        {
            Title = "用于代码审查的自定义代理与模型配置",
            Subtitle = "复用项目约定，检查实现、测试与潜在风险。",
            Metadata = "OpenAI / gpt-5.4-pro",
            BadgeText = "默认 · 已启用",
            PrimaryLabel = "编辑",
            SecondaryLabel = "删除",
            ActionTarget = new object(),
            PrimaryCommand = new RelayCommand(() => { }),
            SecondaryCommand = new RelayCommand(() => { }),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var window = Window(row, dark, width, 180);
        window.Show();
        try
        {
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            Save(window, FormattableString.Invariant($"settings-row-{dark}-{width}-{scale}x"));
            var title = row.FindControl<AppText>("TitleText")!;
            var primary = row.FindControl<AppButton>("PrimaryActionButton")!;
            var secondary = row.FindControl<AppButton>("SecondaryActionButton")!;
            Assert.True(title.Bounds.Width >= 120, $"Title collapsed to {title.Bounds.Width} pixels");
            AssertContained(primary, row);
            AssertContained(secondary, row);
            Assert.True(Right(title, row) <= Origin(primary, row).X);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ComposerFocus_UsesThemeFeedbackWithoutLayoutShift(bool dark) => _session.Dispatch(() =>
    {
        var view = new WorkspaceComposerView { DataContext = ComposerContext() };
        var outside = new AppButton { Content = "焦点对照" };
        var host = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { view, outside } };
        Grid.SetRow(outside, 1);
        var window = Window(host, dark, 460, 340);
        window.Show();
        try
        {
            Assert.True(outside.Focus());
            window.UpdateLayout();
            var surface = Assert.Single(view.GetVisualDescendants().OfType<AppCard>(), card => card.Classes.Contains("composer-surface"));
            var idle = Assert.IsAssignableFrom<ISolidColorBrush>(surface.BorderBrush).Color;
            var bounds = surface.Bounds;
            var thickness = surface.BorderThickness;
            var input = Assert.Single(view.GetVisualDescendants().OfType<AppComposerInput>());
            Assert.True(input.Focus());
            window.UpdateLayout();
            Assert.NotEqual(idle, Assert.IsAssignableFrom<ISolidColorBrush>(surface.BorderBrush).Color);
            Assert.Equal(bounds, surface.Bounds);
            Assert.Equal(thickness, surface.BorderThickness);
            Save(window, $"composer-focused-{dark}");
            Assert.True(outside.Focus());
            window.UpdateLayout();
            Assert.Equal(idle, Assert.IsAssignableFrom<ISolidColorBrush>(surface.BorderBrush).Color);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static object ComposerContext() => new
    {
        Catalog = new { SelectedProject = new ProjectProfile(Guid.NewGuid(), "MossAgent", "D:\\Example", ["D:\\Example"], DateTimeOffset.UtcNow) },
        RepositoryStatus = new { BranchLabel = "feature/interface-polish" },
        AgentPicker = new { CanChoose = false, HasCatalogNotice = false },
        Attachments = new { HasItems = false, Status = string.Empty, AddFilesCommand = new RelayCommand(() => { }) },
        Queue = new { IsAvailable = false, IsVisible = false },
        ModelPicker = new
        {
            ComposerDisplayName = "OpenAI / GPT-5.4 Pro — extended context development model",
            ReasoningDisplayName = "extra-high-custom-reasoning",
            CurrentDisplayName = "OpenAI / GPT-5.4 Pro — extended context development model · extra-high-custom-reasoning",
            ShowQuickMenuCommand = new RelayCommand(() => { }),
            HasReasoningError = false
        },
        ComposerText = "请检查当前更改，并给出精简的修改建议。",
        PrimaryActionCommand = new RelayCommand(() => { }),
        PrimaryActionLabel = "发送消息",
        ApprovalPolicy = ApprovalPolicy.AskEveryTime,
        SetApprovalPolicyCommand = new RelayCommand<ApprovalPolicy>(_ => { }),
        HasAgents = false,
        HasActiveRun = false,
        IsRunning = false,
        HasSendPreparationError = false
    };

    private static AppWindow Window(Control content, bool dark, int width, int height) => new()
    {
        Width = width,
        Height = height,
        Content = content,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static void Save(AppWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    private static Point Origin(Control control, Control relative) => control.TranslatePoint(default, relative)!.Value;
    private static double Right(Control control, Control relative) => Origin(control, relative).X + control.Bounds.Width;

    private static void AssertContained(Control control, Control surface)
    {
        var origin = Origin(control, surface);
        Assert.InRange(origin.X, 0, surface.Bounds.Width);
        Assert.InRange(Right(control, surface), 0, surface.Bounds.Width);
        Assert.InRange(origin.Y, 0, surface.Bounds.Height);
        Assert.InRange(origin.Y + control.Bounds.Height, 0, surface.Bounds.Height);
    }
}
