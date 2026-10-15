using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ApprovalPolicyButtonComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly, false, "UiIconLock")]
    [InlineData(ApprovalPolicy.ReadOnly, true, "UiIconLock")]
    [InlineData(ApprovalPolicy.AskEveryTime, false, "UiIconShield")]
    [InlineData(ApprovalPolicy.AskEveryTime, true, "UiIconShield")]
    [InlineData(ApprovalPolicy.FullAccess, false, "UiIconWarning")]
    [InlineData(ApprovalPolicy.FullAccess, true, "UiIconWarning")]
    public Task Indicator_UsesOneCenteredSharedIcon(ApprovalPolicy policy, bool dark, string resource) => _session.Dispatch(() =>
    {
        var button = new AppApprovalPolicyButton { Policy = policy };
        var window = new AppWindow
        {
            Width = 320,
            Height = 120,
            Content = button,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
            var text = Assert.Single(button.GetVisualDescendants().OfType<AppText>());
            Assert.Equal(12, icon.Size);
            Assert.True(icon.IsEffectivelyVisible);
            Assert.Same(window.FindResource(resource), icon.Data);
            Assert.Equal(ApprovalPolicyText.Label(policy), text.Text);
            Assert.Equal(text.Text, AutomationProperties.GetName(button));
            var iconPosition = icon.TranslatePoint(default, button)!.Value;
            var textPosition = text.TranslatePoint(default, button)!.Value;
            Assert.InRange(Math.Abs(iconPosition.Y + icon.Bounds.Height / 2
                - textPosition.Y - text.Bounds.Height / 2), 0, 0.5);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Menu_UsesExistingWorkspaceCommandAndUpdatesWithoutReconstruction() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
        var window = new AppWindow { Width = 740, Height = 300, Content = view };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = Assert.Single(view.GetVisualDescendants().OfType<AppApprovalPolicyButton>());
            var flyout = Assert.IsType<AppFlyout>(button.Flyout);
            Assert.True(flyout.DismissOnButtonClick);
            var surface = Assert.IsType<AppCard>(flyout.Content);
            var layout = Assert.IsType<Avalonia.Controls.StackPanel>(surface.Child);
            var options = layout.Children.OfType<AppButton>().ToArray();
            Assert.Equal(3, options.Length);
            foreach (var policy in Enum.GetValues<ApprovalPolicy>())
            {
                var row = Assert.Single(options, option => Equals(option.CommandParameter, policy));
                Assert.Same(fixture.Workspace.SetApprovalPolicyCommand, row.Command);
                Assert.True(row.IsEffectivelyEnabled);
                row.Command!.Execute(row.CommandParameter);
                window.UpdateLayout();
                Assert.Equal(policy, fixture.Workspace.ApprovalPolicy);
                Assert.Equal(policy, button.Policy);
                Assert.Equal(fixture.Workspace.ApprovalPolicyLabel, AutomationProperties.GetName(button));
                Assert.Contains("selected", row.Classes);
                Assert.Single(options, option => option.Classes.Contains("selected"));
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingOrRejectedCommand_DisablesOptionsAndPreservesPolicy(bool rejected) => _session.Dispatch(() =>
    {
        var calls = 0;
        var button = new AppApprovalPolicyButton
        {
            Policy = ApprovalPolicy.ReadOnly,
            SelectPolicyCommand = rejected ? new RelayCommand<ApprovalPolicy>(_ => calls++, _ => false) : null
        };
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        var window = new AppWindow { Width = 400, Height = 300, Content = button };
        window.Show();
        try
        {
            flyout.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var surface = Assert.IsType<AppCard>(flyout.Content);
            var options = Assert.IsType<StackPanel>(surface.Child).Children.OfType<AppButton>().ToArray();
            Assert.Equal(3, options.Length);
            Assert.All(options, option => Assert.False(option.IsEffectivelyEnabled));
            Assert.Equal(0, calls);
            Assert.Equal(ApprovalPolicy.ReadOnly, button.Policy);
        }
        finally { flyout.Hide(); window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ExplicitAccessibleName_SurvivesPolicyChanges() => _session.Dispatch(() =>
    {
        var button = new AppApprovalPolicyButton();
        button.Bind(AutomationProperties.NameProperty, new Binding { Source = "自定义权限名称" });
        button.Policy = ApprovalPolicy.FullAccess;
        Assert.Equal("自定义权限名称", AutomationProperties.GetName(button));
    }, TestContext.Current.CancellationToken);
}
