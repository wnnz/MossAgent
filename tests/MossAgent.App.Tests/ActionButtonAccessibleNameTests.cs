using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ActionButtonAccessibleNameTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExplicitName_IsPreservedRegardlessOfPropertyOrder(bool nameFirst) => _session.Dispatch(() =>
    {
        var button = new AppActionButton();
        if (nameFirst) AutomationProperties.SetName(button, "选择模型");
        button.Label = "中";
        if (!nameFirst) AutomationProperties.SetName(button, "选择模型");
        button.Label = "高";
        button.BadgeText = "已选择";
        Assert.Equal("选择模型", AutomationProperties.GetName(button));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task BoundName_ContinuesUpdatingAfterLabelAndBadgeChanges() => _session.Dispatch(() =>
    {
        var source = new AppInput { Text = "模型操作" };
        var button = new AppActionButton();
        button.Bind(AutomationProperties.NameProperty, new Binding(nameof(TextBox.Text)) { Source = source });
        button.Label = "中";
        button.BadgeText = "已选择";
        Assert.Equal("模型操作", AutomationProperties.GetName(button));
        source.Text = "更新后的操作";
        button.Label = "高";
        Assert.Equal("更新后的操作", AutomationProperties.GetName(button));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingExplicitName_UsesTheCurrentLabelAndBadgeAsFallback() => _session.Dispatch(() =>
    {
        var button = new AppActionButton { Label = "模型", BadgeText = "高" };
        Assert.Equal("模型，高", AutomationProperties.GetName(button));
        AutomationProperties.SetName(button, "操作");
        button.Label = "其他模型";
        button.ClearValue(AutomationProperties.NameProperty);
        Assert.Equal("其他模型，高", AutomationProperties.GetName(button));
        button.BadgeText = null;
        Assert.Equal("其他模型", AutomationProperties.GetName(button));
    }, TestContext.Current.CancellationToken);
}
