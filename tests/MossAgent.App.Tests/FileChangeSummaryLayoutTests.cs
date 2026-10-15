using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileChangeSummaryLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280)]
    [InlineData(false, 740)]
    [InlineData(true, 280)]
    [InlineData(true, 740)]
    public async Task Summary_UsesBoundedRowsSharedGeometryAndExplicitDisabledUndo(bool dark, int width)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await _session.Dispatch(() =>
        {
            var workspace = fixture.Workspace;
            for (var index = 0; index < 5; index++)
                workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("filesystem.write_file", true, "已写入", null,
                    FileChanges: [new(Path.Combine(fixture.Project.PrimaryDirectory, $"a-very-long-directory-name/file-{index}.cs"), null, new string('B', 64))])));
            var view = new AppFileChangeSummary { Summary = workspace.FileChanges };
            var window = new AppWindow
            {
                Width = width,
                Height = 420,
                Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var descendants = view.GetVisualDescendants().ToArray();
                var rows = descendants.OfType<AppButton>().Where(button => button.Classes.Contains("file-change-file")).ToArray();
                Assert.Equal(3, rows.Length);
                foreach (var row in rows)
                {
                    var origin = row.TranslatePoint(default, view)!.Value;
                    Assert.InRange(origin.X + row.Bounds.Width, 0, view.Bounds.Width);
                    Assert.True(row.Bounds.Height >= 36);
                    Assert.Same(workspace.FileChanges.OpenFileCommand, row.Command);
                }
                var undo = Assert.Single(descendants.OfType<AppButton>(), button => button.Classes.Contains("file-change-action"));
                Assert.False(undo.IsEffectivelyEnabled);
                Assert.False(undo.Command!.CanExecute(null));
                Assert.Same(workspace.FileChanges.Undo.RequestCommand, undo.Command);
                var icon = Assert.Single(descendants.OfType<AppIcon>(), item => item.Classes.Contains("file-change-summary-icon"));
                Assert.Equal(18, icon.Size);
                Assert.Equal(18, icon.Bounds.Width);
                var surface = Assert.Single(descendants.OfType<AppBorder>(), item => item.Classes.Contains("file-change-surface"));
                Assert.Equal(new CornerRadius(12), surface.CornerRadius);
                Assert.True(surface.BoxShadow.Count > 0);
                workspace.FileChanges.ExpandCommand.Execute(null);
                window.UpdateLayout();
                Assert.Equal(5, view.GetVisualDescendants().OfType<AppButton>().Count(button => button.Classes.Contains("file-change-file")));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false, 280)]
    [InlineData(false, 740)]
    [InlineData(true, 280)]
    [InlineData(true, 740)]
    public async Task UndoConfirmation_WrapsWithinSharedSurfaceAndBindsRealConfirmAndCancel(bool dark, int width)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        await _session.DispatchAsync(async () =>
        {
            var workspace = fixture.Workspace;
            await workspace.LoadAsync();
            workspace.SetApprovalPolicyCommand.Execute(MossAgent.Domain.ApprovalPolicy.FullAccess);
            var before = new string('A', 64);
            var after = new string('B', 64);
            workspace.Messages.Add(ChatMessageViewModel.CreateTool(new("filesystem.write_file", true, "已写入", null,
                FileChanges: [new(Path.Combine(fixture.Project.PrimaryDirectory, "sample.cs"), before, after,
                    new("edit-00000000000000000000000000000001.snapshot", 0, before),
                    new("edit-00000000000000000000000000000002.snapshot", 0, after))])));
            var summary = workspace.FileChanges;
            Assert.True(summary.Undo.RequestCommand.CanExecute(null));
            summary.Undo.RequestCommand.Execute(null);
            var view = new AppFileChangeSummary { Summary = summary };
            var window = new AppWindow
            {
                Width = width,
                Height = 460,
                Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                var buttons = view.GetVisualDescendants().OfType<AppButton>().ToArray();
                var confirm = Assert.Single(buttons, button => ReferenceEquals(button.Command, summary.Undo.ConfirmCommand));
                var cancel = Assert.Single(buttons, button => ReferenceEquals(button.Command, summary.Undo.CancelCommand));
                Assert.True(confirm.IsEnabled);
                foreach (var button in new[] { confirm, cancel })
                {
                    var origin = button.TranslatePoint(default, view)!.Value;
                    Assert.InRange(origin.X + button.Bounds.Width, 0, view.Bounds.Width);
                    Assert.Equal(28, button.Bounds.Height);
                }
                summary.Undo.CancelCommand.Execute(null);
                Assert.False(summary.Undo.IsConfirming);
                Assert.False(summary.Undo.ConfirmCommand.CanExecute(null));
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }
}
