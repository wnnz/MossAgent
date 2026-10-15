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
public sealed class ProjectPromptComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Heading_ContainsLongLabelsAndPreservesProjectActionAcrossResize(bool dark) => _session.Dispatch(() =>
    {
        var calls = 0;
        var flyout = new AppFlyout { Content = new AppText { Text = "合成项目菜单" } };
        var prompt = new AppProjectPrompt
        {
            Prefix = "你想让我们在 ",
            Suffix = " 中做些什么？",
            ProjectName = new string('项', 80),
            SwitchLabel = "切换项目",
            SelectProjectCommand = new RelayCommand(() => calls++),
            ProjectFlyout = flyout
        };
        var window = new AppWindow
        {
            Width = 980,
            Height = 460,
            Content = prompt,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            foreach (var width in new[] { 980, 360, 260, 980 })
            {
                window.Width = width;
                window.UpdateLayout();
                var labels = prompt.GetVisualDescendants().OfType<AppText>().ToArray();
                Assert.Equal(3, labels.Length);
                foreach (var label in labels)
                {
                    Assert.True(label.IsEffectivelyVisible);
                    var position = label.TranslatePoint(default, prompt)!.Value;
                    Assert.InRange(position.X, 0, prompt.Bounds.Width);
                    Assert.True(position.X + label.Bounds.Width <= prompt.Bounds.Width);
                    Assert.InRange(label.Bounds.Height, 1, prompt.Bounds.Height);
                }
                var button = Assert.Single(prompt.GetVisualDescendants().OfType<AppButton>());
                var positionOfButton = button.TranslatePoint(default, prompt)!.Value;
                Assert.True(positionOfButton.X >= 0 && positionOfButton.X + button.Bounds.Width <= prompt.Bounds.Width);
                Assert.Same(flyout, button.Flyout);
                Assert.Equal("切换项目", ToolTip.GetTip(button));
                Assert.True(button.IsEffectivelyEnabled);
                if (width == 980)
                {
                    var prefix = Assert.Single(labels, label => label.Text == prompt.Prefix);
                    var suffix = Assert.Single(labels, label => label.Text == prompt.Suffix);
                    var prefixPosition = prefix.TranslatePoint(default, prompt)!.Value;
                    var suffixPosition = suffix.TranslatePoint(default, prompt)!.Value;
                    Assert.InRange(Math.Abs(prefixPosition.X + prefix.Bounds.Width - positionOfButton.X), 0, 1);
                    Assert.InRange(Math.Abs(suffixPosition.X - positionOfButton.X - button.Bounds.Width), 0, 1);
                }
            }
            var project = Assert.Single(prompt.GetVisualDescendants().OfType<AppButton>());
            project.Command!.Execute(null);
            Assert.Equal(1, calls);
            prompt.ProjectName = "更新后的项目";
            window.UpdateLayout();
            Assert.Contains(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.ProjectName);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingCommand_DisablesProjectAction() => _session.Dispatch(() =>
    {
        var prompt = new AppProjectPrompt { ProjectName = "未配置操作的项目" };
        var layout = Assert.IsAssignableFrom<Panel>(prompt.Content);
        Assert.False(Assert.Single(layout.Children.OfType<AppButton>()).IsEffectivelyEnabled);
    }, TestContext.Current.CancellationToken);
}
