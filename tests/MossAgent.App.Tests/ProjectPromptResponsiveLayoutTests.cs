using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectPromptResponsiveLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(600, false)]
    [InlineData(601, false)]
    [InlineData(664, false)]
    [InlineData(980, false)]
    [InlineData(600, true)]
    [InlineData(601, true)]
    [InlineData(664, true)]
    [InlineData(980, true)]
    public Task ShortHeading_UsesOneRowWheneverItsStyledContentFits(double width, bool dark) =>
        WithPrompt(width, dark, (window, prompt) =>
        {
            AssertInline(prompt);
            AssertInside(prompt);
            var button = ProjectButton(prompt);
            var prefix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Prefix);
            var suffix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Suffix);
            Assert.InRange(Math.Abs(CenterY(prefix, prompt) - CenterY(button, prompt)), 0, 0.5);
            Assert.InRange(Math.Abs(CenterY(suffix, prompt) - CenterY(button, prompt)), 0, 0.5);
            Assert.Equal(27.5, prefix.FontSize);
            Assert.Equal(27.5, suffix.FontSize);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NameChangesAndResize_ReevaluateFitWithoutReplacingTheProjectAction(bool dark) =>
        WithPrompt(600, dark, (window, prompt) =>
        {
            var button = ProjectButton(prompt);
            AssertInline(prompt);
            prompt.ProjectName = new string('项', 80);
            Settle(window);
            AssertWrapped(prompt);
            AssertInside(prompt);
            window.Width = 980;
            Settle(window);
            AssertInline(prompt);
            window.Width = 360;
            Settle(window);
            AssertWrapped(prompt);
            AssertInside(prompt);
            prompt.ProjectName = "MossAgent";
            window.Width = 600;
            Settle(window);
            AssertInline(prompt);
            Assert.Same(button, ProjectButton(prompt));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProjectAction_RetainsKeyboardCommandAndDisabledStateThroughReflow(bool dark) =>
        WithPrompt(600, dark, (window, prompt) =>
        {
            var calls = 0;
            var enabled = true;
            var command = new RelayCommand(() => calls++, () => enabled);
            prompt.SelectProjectCommand = command;
            var button = ProjectButton(prompt);
            foreach (var width in new[] { 600, 260, 600 })
            {
                window.Width = width;
                Settle(window);
                AssertInside(prompt);
                Assert.True(button.Focus(NavigationMethod.Tab));
                button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
                button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            }
            Assert.Equal(3, calls);
            enabled = false;
            command.NotifyCanExecuteChanged();
            window.UpdateLayout();
            Assert.False(button.IsEffectivelyEnabled);
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
            button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
            Assert.Equal(3, calls);
            prompt.SelectProjectCommand = null;
            window.UpdateLayout();
            Assert.False(button.IsEffectivelyEnabled);
        });

    private Task WithPrompt(double width, bool dark, Action<AppWindow, AppProjectPrompt> check) => _session.Dispatch(() =>
    {
        var prompt = new AppProjectPrompt
        {
            Prefix = "你想让我们在 ",
            Suffix = " 中构建什么？",
            ProjectName = "MossAgent",
            SelectProjectCommand = new RelayCommand(() => { })
        };
        var window = new AppWindow
        {
            Width = width,
            Height = 460,
            Content = prompt,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { Settle(window); check(window, prompt); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppButton ProjectButton(AppProjectPrompt prompt) => Assert.Single(prompt.GetVisualDescendants().OfType<AppButton>());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExtremelyNarrowWidth_StacksAllFragmentsInsideTheViewport(bool dark) =>
        WithPrompt(180, dark, (window, prompt) =>
        {
            AssertInside(prompt);
            var prefix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Prefix);
            var suffix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Suffix);
            var project = ProjectButton(prompt);
            Assert.True(project.TranslatePoint(default, prompt)!.Value.Y >= prefix.TranslatePoint(default, prompt)!.Value.Y + prefix.Bounds.Height);
            Assert.True(suffix.TranslatePoint(default, prompt)!.Value.Y >= project.TranslatePoint(default, prompt)!.Value.Y + project.Bounds.Height);
        });

    private static void AssertInline(AppProjectPrompt prompt)
    {
        var prefix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Prefix);
        var suffix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Suffix);
        var project = ProjectButton(prompt);
        Assert.InRange(Math.Abs(CenterY(prefix, prompt) - CenterY(project, prompt)), 0, 0.5);
        Assert.True(Math.Abs(CenterY(suffix, prompt) - CenterY(project, prompt)) <= 0.5,
            $"Suffix center {CenterY(suffix, prompt)}; project center {CenterY(project, prompt)}; prompt {prompt.Bounds}; layout {((Control)prompt.Content!).Bounds} desired {((Control)prompt.Content!).DesiredSize}");
        Assert.True(prefix.TranslatePoint(default, prompt)!.Value.X + prefix.Bounds.Width <= project.TranslatePoint(default, prompt)!.Value.X + 0.5,
            $"Prefix {prefix.TranslatePoint(default, prompt)} {prefix.Bounds.Size} desired {prefix.DesiredSize}; project {project.TranslatePoint(default, prompt)} {project.Bounds.Size} desired {project.DesiredSize}; layout {((Control)prompt.Content!).Bounds} desired {((Control)prompt.Content!).DesiredSize}");
        Assert.True(project.TranslatePoint(default, prompt)!.Value.X + project.Bounds.Width <= suffix.TranslatePoint(default, prompt)!.Value.X + 0.5);
    }

    private static void AssertWrapped(AppProjectPrompt prompt)
    {
        var prefix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Prefix);
        var suffix = Assert.Single(prompt.GetVisualDescendants().OfType<AppText>(), text => text.Text == prompt.Suffix);
        var project = ProjectButton(prompt);
        Assert.InRange(Math.Abs(CenterY(prefix, prompt) - CenterY(project, prompt)), 0, 0.5);
        Assert.True(prefix.TranslatePoint(default, prompt)!.Value.X + prefix.Bounds.Width <= project.TranslatePoint(default, prompt)!.Value.X + 0.5,
            $"Prefix {prefix.TranslatePoint(default, prompt)} {prefix.Bounds.Size} desired {prefix.DesiredSize}; project {project.TranslatePoint(default, prompt)} {project.Bounds.Size} desired {project.DesiredSize}; layout {((Control)prompt.Content!).Bounds} desired {((Control)prompt.Content!).DesiredSize}");
        Assert.True(suffix.TranslatePoint(default, prompt)!.Value.Y >= project.TranslatePoint(default, prompt)!.Value.Y + project.Bounds.Height - 0.5,
            $"Suffix {suffix.TranslatePoint(default, prompt)} {suffix.Bounds.Size}; project {project.TranslatePoint(default, prompt)} {project.Bounds.Size}; prompt {prompt.Bounds.Size}");
    }

    private static void AssertInside(AppProjectPrompt prompt)
    {
        foreach (var control in prompt.GetVisualDescendants().Where(control => control is AppText or AppButton).OfType<Control>())
        {
            var position = control.TranslatePoint(default, prompt)!.Value;
            Assert.InRange(position.X, 0, prompt.Bounds.Width);
            Assert.True(position.X + control.Bounds.Width <= prompt.Bounds.Width + 0.5);
        }
    }

    private static double CenterY(Control control, Control root) =>
        control.TranslatePoint(default, root)!.Value.Y + control.Bounds.Height / 2;

    private static void Settle(AppWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
