using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

internal static class ProviderUiWorkflowInput
{
    public static T Named<T>(MainWindow window, string name) where T : Control =>
        Assert.Single(window.GetVisualDescendants().OfType<T>(), control =>
            control.IsEffectivelyVisible && AutomationProperties.GetName(control) == name);

    public static AppButton Button(MainWindow window, string text) =>
        Assert.Single(window.GetVisualDescendants().OfType<AppButton>(), button =>
            button.IsEffectivelyVisible && Equals(button.Content, text));

    public static AppIconButton SendButton(MainWindow window) =>
        Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(), button => button.Name == "SendButton");

    public static void Check(MainWindow window, string label, bool value)
    {
        var checkbox = Assert.Single(window.GetVisualDescendants().OfType<AppCheckBox>(), control =>
            control.IsEffectivelyVisible && Equals(control.Content, label));
        if (checkbox.IsChecked != value) Press(window, checkbox);
    }

    public static void Press(MainWindow window, Control control)
    {
        control.BringIntoView();
        Flush(window);
        Assert.True(control.IsEffectivelyVisible);
        Assert.True(control.IsEffectivelyEnabled);
        Assert.True(control.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Flush(window);
    }

    public static void Type(MainWindow window, AppInput input, string text)
    {
        input.BringIntoView();
        Flush(window);
        Assert.True(input.Focus(NavigationMethod.Tab));
        input.SelectAll();
        if (text.Length == 0)
        {
            window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
            window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
        }
        else window.KeyTextInput(text);
        Flush(window);
        Assert.Equal(text, input.Text);
    }

    public static AppProviderSettingsHeader Header(ProviderUiWorkflowFixture fixture, Guid providerId) =>
        Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppProviderSettingsHeader>(),
            header => header.Provider?.Id == providerId);

    public static async Task FetchAsync(ProviderUiWorkflowFixture fixture, Guid providerId)
    {
        var header = Header(fixture, providerId);
        if (!header.IsExpanded) Press(fixture.Window, header.FindControl<AppIconButton>("ToggleModelsButton")!);
        var fetch = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppButton>(), button =>
            button.IsEffectivelyVisible && Equals(button.Content, "获取模型")
            && button.DataContext is ProviderModelGroupViewModel group && group.Provider.Id == providerId);
        Press(fixture.Window, fetch);
        var execution = fixture.Main.Models.FetchModelsForProviderCommand.ExecutionTask;
        Assert.NotNull(execution);
        await execution.WaitAsync(fixture.Token);
        Flush(fixture.Window);
    }

    public static void Edit(ProviderUiWorkflowFixture fixture, Guid modelId)
    {
        var row = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppModelSettingsRow>(),
            candidate => candidate.Model?.Id == modelId);
        Press(fixture.Window, row.FindControl<AppButton>("EditModelButton")!);
        Assert.True(fixture.Main.Models.IsFormOpen);
    }

    public static async Task SaveModelAsync(ProviderUiWorkflowFixture fixture)
    {
        Press(fixture.Window, Button(fixture.Window, "保存模型"));
        await fixture.Main.Models.SaveModelCommand.ExecutionTask!.WaitAsync(fixture.Token);
        Flush(fixture.Window);
    }

    public static async Task SetProviderEnabledAsync(ProviderUiWorkflowFixture fixture, Guid providerId, bool enabled)
    {
        Press(fixture.Window, Header(fixture, providerId).FindControl<AppButton>("EditProviderButton")!);
        var toggle = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppCheckBox>(),
            checkbox => checkbox.IsEffectivelyVisible && Equals(checkbox.Content, "启用"));
        if (toggle.IsChecked != enabled) Press(fixture.Window, toggle);
        Press(fixture.Window, Button(fixture.Window, "保存提供商"));
        await fixture.Main.Providers.SaveCommand.ExecutionTask!.WaitAsync(fixture.Token);
        Flush(fixture.Window);
        Assert.False(fixture.Main.Providers.IsFormOpen);
    }

    public static void AssertAnswerVisible(MainWindow window)
    {
        Flush(window);
        var answer = Assert.Single(window.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
            control => control.IsEffectivelyVisible && control.Markdown == "hello");
        answer.BringIntoView();
        Flush(window);
        var point = answer.TranslatePoint(new Point(4, answer.Bounds.Height / 2), window)!.Value;
        Assert.True(new Rect(window.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
        Assert.True(ReferenceEquals(answer, hit) || answer.IsVisualAncestorOf(hit));
    }

    public static void Flush(MainWindow window) => HeadlessRenderedFrame.Flush(window);
}
