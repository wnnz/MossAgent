using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspacePaneAdaptiveContentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WiderArrangement_RemeasuresAdaptiveContentInsideActualPrimaryWidth(bool dark) => WithPane(dark, (panes, prompt) =>
    {
        Arrange(panes, 660, 1600);
        Assert.Equal(844, panes.PrimaryPane!.Bounds.Width);
        Assert.Equal(812, prompt.Bounds.Width);
        AssertLayout(prompt, compact: false);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NarrowerArrangement_ContainsAllPromptFragmentsWithoutClipping(bool dark) => WithPane(dark, (panes, prompt) =>
    {
        Arrange(panes, 1600, 660);
        Assert.Equal(360, panes.PrimaryPane!.Bounds.Width);
        Assert.Equal(328, prompt.Bounds.Width);
        AssertLayout(prompt, compact: true);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RepeatedArrangement_PreservesPromptAndItsExistingAction(bool dark) => WithPane(dark, (panes, prompt) =>
    {
        var primary = panes.PrimaryPane;
        var inspector = panes.InspectorPane;
        var command = prompt.SelectProjectCommand;
        foreach (var actualWidth in new[] { 1600d, 660d, 1600d })
        {
            Arrange(panes, actualWidth == 1600 ? 660 : 1600, actualWidth);
            AssertLayout(prompt, compact: actualWidth == 660);
            Assert.Same(primary, panes.PrimaryPane);
            Assert.Same(inspector, panes.InspectorPane);
            Assert.Same(command, Assert.Single(prompt.GetVisualDescendants().OfType<AppButton>()).Command);
        }
    });

    private Task WithPane(bool dark, Action<AppWorkspacePanes, AppProjectPrompt> verify) => _session.Dispatch(() =>
    {
        var prompt = new AppProjectPrompt
        {
            Prefix = "你想让我们在 ",
            Suffix = " 中构建什么？",
            ProjectName = "共享布局项目",
            SelectProjectCommand = new RelayCommand(() => { })
        };
        var panes = new AppWorkspacePanes
        {
            PrimaryPane = new Grid { Children = { prompt } },
            InspectorPane = new AppBorder(),
            IsInspectorOpen = true
        };
        var window = new AppWindow
        {
            Width = 1600,
            Height = 820,
            Content = panes,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            verify(panes, prompt);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Arrange(AppWorkspacePanes panes, double measuredWidth, double arrangedWidth)
    {
        panes.InvalidateMeasure();
        panes.Measure(new Size(measuredWidth, 820));
        panes.InvalidateArrange();
        panes.Arrange(new Rect(0, 0, arrangedWidth, 820));
        Assert.Equal(arrangedWidth, panes.Bounds.Width);
    }

    private static void AssertLayout(AppProjectPrompt prompt, bool compact)
    {
        var labels = prompt.GetVisualDescendants().OfType<AppText>().ToArray();
        Assert.Equal(3, labels.Length);
        foreach (var label in labels)
        {
            var bounds = new Rect(label.TranslatePoint(default, prompt)!.Value, label.Bounds.Size);
            Assert.True(label.IsEffectivelyVisible);
            Assert.InRange(bounds.Left, 0, prompt.Bounds.Width);
            Assert.InRange(bounds.Right, 1, prompt.Bounds.Width);
        }
        var prefix = Assert.Single(labels, label => label.Text == prompt.Prefix);
        var name = Assert.Single(labels, label => label.Text == prompt.ProjectName);
        var suffix = Assert.Single(labels, label => label.Text == prompt.Suffix);
        var firstRowCenters = new[] { prefix, name }.Select(label => label.TranslatePoint(default, prompt)!.Value.Y + label.Bounds.Height / 2).ToArray();
        Assert.InRange(firstRowCenters.Max() - firstRowCenters.Min(), 0, 1);
        if (compact)
        {
            Assert.True(suffix.TranslatePoint(default, prompt)!.Value.Y >= prefix.TranslatePoint(default, prompt)!.Value.Y + prefix.Bounds.Height);
        }
        else
        {
            var centers = labels.Select(label => label.TranslatePoint(default, prompt)!.Value.Y + label.Bounds.Height / 2).ToArray();
            Assert.InRange(centers.Max() - centers.Min(), 0, 1);
        }
    }
}
