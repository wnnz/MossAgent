using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal static class AutomationUiWorkflowInput
{
    public static async Task<AutomationSchedule> CreateAsync(AutomationUiWorkflowFixture fixture)
    {
        await fixture.ActivateAsync(fixture.Page.CreateCommand);
        Assert.True(fixture.Page.Editor.IsOpen);
        Type(fixture, AutomationText.Name, "界面创建的项目审查");
        Type(fixture, AutomationText.Prompt, "仅检查这个项目，返回简短结果。");
        Select<ProjectProfile>(fixture, AutomationText.Project, item => item.Id == fixture.Core.Workspace.Catalog.SelectedProject!.Id);
        Select<ModelPickerItemViewModel>(fixture, AutomationText.Model, item => item.Model.Id == fixture.Core.AlternativeModel.Id);
        Select<AutomationChoice<AutomationFrequency>>(fixture, AutomationText.Frequency, item => item.Value == AutomationFrequency.Hourly);
        SetNumber(fixture, AutomationText.Interval, 2);
        SetNumber(fixture, AutomationText.Timeout, 3);
        Assert.NotEqual(AutomationText.Get("PreviewIncomplete"), fixture.Page.Editor.SchedulePreview);
        fixture.Capture("configured-editor");
        await fixture.ActivateAsync(fixture.Page.SaveCommand);
        Assert.False(fixture.Page.HasError, fixture.Page.Error);
        Assert.False(fixture.Page.Editor.IsOpen);
        var stored = Assert.Single(await fixture.Core.Automations.GetAllAsync(fixture.Token));
        Assert.Equal("界面创建的项目审查", stored.Name);
        Assert.Equal("仅检查这个项目，返回简短结果。", stored.Prompt);
        Assert.Equal(fixture.Core.AlternativeModel.ProviderId, stored.ProviderId);
        Assert.Equal(fixture.Core.AlternativeModel.Id, stored.ModelId);
        Assert.Equal(AutomationFrequency.Hourly, stored.Frequency);
        Assert.Equal(2, stored.IntervalHours);
        Assert.Equal(3, stored.TimeoutMinutes);
        Assert.Equal(ApprovalPolicy.ReadOnly, stored.ApprovalPolicy);
        Assert.False(stored.IsEnabled);
        Assert.Empty(fixture.Core.Server.Requests);
        fixture.Capture("saved");
        return stored;
    }

    public static void Type(AutomationUiWorkflowFixture fixture, string label, string text) =>
        ProviderUiWorkflowInput.Type(fixture.Window, Named<AppInput>(fixture, label), text);

    public static void Select<T>(AutomationUiWorkflowFixture fixture, string label, Func<T, bool> predicate)
    {
        var combo = Named<AppComboBox>(fixture, label);
        var options = combo.ItemsView.Cast<T>().ToArray();
        var target = Assert.Single(options, item => predicate(item));
        var index = Array.IndexOf(options, target);
        combo.BringIntoView();
        fixture.Flush();
        Assert.True(combo.Focus(NavigationMethod.Tab));
        Key(fixture, Avalonia.Input.Key.F4, PhysicalKey.F4);
        Assert.True(combo.IsDropDownOpen);
        var item = Assert.IsType<ComboBoxItem>(combo.ContainerFromIndex(index));
        item.BringIntoView();
        fixture.Flush();
        var popup = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(item));
        var point = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), popup)!.Value;
        Assert.True(new Rect(popup.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(popup.InputHitTest(point));
        Assert.True(ReferenceEquals(item, hit) || item.IsVisualAncestorOf(hit));
        popup.MouseDown(point, MouseButton.Left);
        popup.MouseUp(point, MouseButton.Left);
        fixture.Flush();
        Assert.Equal(target, combo.SelectedItem);
        Assert.False(combo.IsDropDownOpen);
    }

    public static void SetNumber(AutomationUiWorkflowFixture fixture, string label, decimal value)
    {
        var number = Named<AppNumericUpDown>(fixture, label);
        number.BringIntoView();
        fixture.Flush();
        var input = Assert.Single(number.GetVisualDescendants().OfType<TextBox>());
        Assert.True(input.Focus(NavigationMethod.Tab));
        input.SelectAll();
        fixture.Window.KeyTextInput(value.ToString(System.Globalization.CultureInfo.CurrentCulture));
        Key(fixture, Avalonia.Input.Key.Tab, PhysicalKey.Tab);
        fixture.Flush();
        Assert.Equal(value, number.Value);
    }

    public static T Named<T>(AutomationUiWorkflowFixture fixture, string label) where T : Control =>
        Assert.Single(fixture.Window.GetVisualDescendants().OfType<T>(),
            control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == label);

    public static void Key(AutomationUiWorkflowFixture fixture, Key key, PhysicalKey physical)
    {
        fixture.Window.KeyPress(key, RawInputModifiers.None, physical, null);
        fixture.Window.KeyRelease(key, RawInputModifiers.None, physical, null);
        fixture.Flush();
    }
}
