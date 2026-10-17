using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class McpSettingsFormAccessibilityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, McpTransportKind.Stdio)]
    [InlineData(true, McpTransportKind.Stdio)]
    [InlineData(false, McpTransportKind.StreamableHttp)]
    [InlineData(true, McpTransportKind.StreamableHttp)]
    public Task EditorFieldsHavePersistentNamesIndependentOfValuesAndTransport(bool dark, McpTransportKind transport) =>
        _session.DispatchAsync(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                await using var services = AppComposition.CreateServices(root);
                await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
                var main = services.GetRequiredService<MainWindowViewModel>();
                var window = new MainWindow
                {
                    DataContext = main,
                    Width = dark ? 526 : 1280,
                    Height = dark ? 601 : 820,
                    RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
                };
                window.Show();
                try
                {
                    main.SelectSettingsTabCommand.Execute("3");
                    ProviderUiWorkflowInput.Flush(window);
                    ProviderUiWorkflowInput.Press(window, Assert.Single(window.GetVisualDescendants().OfType<AppActionButton>(),
                        control => control.IsEffectivelyVisible && ReferenceEquals(control.Command, main.Mcp.OpenCreateFormCommand)));
                    main.Mcp.Transport = transport;
                    ProviderUiWorkflowInput.Flush(window);
                    AssertName<AppInput>(window, "Server 名称");
                    AssertName<AppComboBox>(window, "传输协议");
                    foreach (var label in transport == McpTransportKind.Stdio
                        ? new[] { "stdio 命令", "参数 JSON 数组", "工作目录", "环境变量 JSON 对象" }
                        : new[] { "Streamable HTTP URL", "HTTP 请求头 JSON 对象" })
                    {
                        var input = AssertName<AppInput>(window, label);
                        input.Text = "synthetic value";
                        Assert.Equal(label, AutomationProperties.GetName(input));
                    }
                    if (transport == McpTransportKind.StreamableHttp) AssertName<AppComboBox>(window, "HTTP 网络代理");
                    Assert.True(main.Mcp.IsFormOpen);
                }
                finally { window.Close(); }
            }
            finally
            {
                SqliteTestPool.Clear(root);
                Directory.Delete(root, recursive: true);
            }
        }, TestContext.Current.CancellationToken);

    private static T AssertName<T>(MainWindow window, string label) where T : Avalonia.Controls.Control =>
        Assert.Single(window.GetVisualDescendants().OfType<T>(), control => control.IsEffectivelyVisible
            && AutomationProperties.GetName(control) == label);
}
