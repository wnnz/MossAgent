using Avalonia.Automation;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SourceAccountSettingsWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SharedDialogSavesRestoresAndActuallyReauthenticates(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var model = fixture.Model;
        var view = new SourceAccountSettingsPane { DataContext = model };
        var window = new Window { Content = view, Width = 980, Height = 640,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var add = Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(), control => control.Label == SourceAccountText.Add);
            Press(add);
            window.UpdateLayout();
            Assert.True(model.Editor.IsOpen);
            var dialog = Assert.Single(view.GetVisualDescendants().OfType<AppSettingsDialog>());
            Assert.True(dialog.IsEffectivelyVisible);
            Input(view, SourceAccountText.Name).Text = "GitHub fixture";
            Input(view, SourceAccountText.Token).Text = "synthetic-ui-source-credential";
            Assert.Equal('●', Input(view, SourceAccountText.Token).PasswordChar);
            var save = Assert.Single(dialog.GetVisualDescendants().OfType<AppButton>(), button => button.Command == model.SaveCommand);
            Press(save);
            await model.SaveCommand.ExecutionTask!;
            window.UpdateLayout();
            Assert.False(model.HasStatusError, model.Status);
            Assert.False(model.Editor.IsOpen);
            Assert.Empty(model.Editor.AccessToken);
            var row = Assert.Single(model.Items);
            var stored = Assert.Single(await fixture.Accounts.GetAllAsync(fixture.Token));
            Assert.Equal("synthetic-ui-source-credential", stored.AccessToken);
            Press(Action(view, model.VerifyCommand));
            await model.VerifyCommand.ExecutionTask!;
            Assert.Contains("fixture-user-0", row.Status, StringComparison.Ordinal);
            Press(Action(view, model.VerifyCommand));
            await model.VerifyCommand.ExecutionTask!;
            Assert.Contains("fixture-user-1", row.Status, StringComparison.Ordinal);
            await fixture.Server.Completion;
            Assert.Equal(2, fixture.Transport.RequestCount);
            Assert.All(fixture.Server.Requests, request => Assert.Contains(request,
                header => header == "Authorization: Bearer synthetic-ui-source-credential"));
            Press(Action(view, model.OpenEditFormCommand));
            await model.OpenEditFormCommand.ExecutionTask!;
            Assert.Equal(stored.AccessToken, model.Editor.AccessToken);
            model.CloseFormCommand.Execute(null);
            Assert.Empty(model.Editor.AccessToken);
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<AppText>(),
                text => text.Text?.Contains(stored.AccessToken, StringComparison.Ordinal) == true);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProductionSettingsNavigationFindsSourceAccountPage() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var main = fixture.Main;
        Assert.Same(fixture.Model, main.SourceAccounts);
        main.SettingsSearchQuery = "GitHub";
        Assert.True(main.IsSourceAccountSettingsMatch);
        main.SelectSettingsTabCommand.Execute("7");
        Assert.True(main.IsSourceAccountSettingsSelected);
        Assert.True(main.IsSettingsOpen);
        Assert.False(main.Workspace.IsRunning);
        Assert.Equal(0, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    private static AppInput Input(Control root, string name) => Assert.Single(root.GetVisualDescendants().OfType<AppInput>(),
        control => AutomationProperties.GetName(control) == name);

    private static AppButton Action(Control root, ICommand command) => Assert.Single(
        root.GetVisualDescendants().OfType<AppButton>(), control => ReferenceEquals(control.Command, command));

    private static void Press(Control control)
    {
        Assert.True(control.Focus(NavigationMethod.Tab));
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
