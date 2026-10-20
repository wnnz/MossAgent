using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class SourceAccountUiFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly HttpClient _loopback = new(new SocketsHttpHandler { UseProxy = false });
    private ServiceProvider? _services;
    private SourceAccountHttpServer? _server;
    public CancellationToken Token => TestContext.Current.CancellationToken;
    public SourceAccountHttpProbe Transport { get; } = new();
    public SourceAccountSettingsViewModel Model => _services!.GetRequiredService<SourceAccountSettingsViewModel>();
    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public ISourceAccountRepository Accounts => _services!.GetRequiredService<ISourceAccountRepository>();
    public IConfigurationRepository Configurations => _services!.GetRequiredService<IConfigurationRepository>();
    public ISourceReviewSessions ReviewSessions => _services!.GetRequiredService<ISourceReviewSessions>();
    public ISourceRepositoryGrants Grants => _services!.GetRequiredService<ISourceRepositoryGrants>();
    public IToolExecutionAuditReader Audit => _services!.GetRequiredService<IToolExecutionAuditReader>();
    public SourceAccountHttpServer Server => _server!;
    public T Get<T>() where T : notnull => _services!.GetRequiredService<T>();

    public static async Task<SourceAccountUiFixture> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var fixture = new SourceAccountUiFixture();
        try
        {
            var services = AppComposition.CreateServiceCollection(fixture._root);
            services.RemoveAll<ISourceAccountHttpClientFactory>();
            services.AddSingleton<ISourceAccountHttpClientFactory>(fixture.Transport);
            configure?.Invoke(services);
            fixture._services = services.BuildServiceProvider(validateScopes: true);
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            fixture._server = new SourceAccountHttpServer(index => SourceAccountHttpServer.JsonResponse(
                $"{{\"id\":42,\"login\":\"fixture-user-{index}\"}}"), 2, fixture.Token);
            fixture.Transport.RespondAsync = fixture.ForwardAsync;
            await fixture.Model.Initialization;
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task<HttpResponseMessage> ForwardAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("https://api.github.com/user", request.RequestUri!.AbsoluteUri);
        using var forwarded = new HttpRequestMessage(request.Method, new Uri(Server.Address, "user"));
        foreach (var header in request.Headers) forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return await _loopback.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        _loopback.Dispose();
        if (_server is not null) await _server.DisposeAsync();
        SqliteTestPool.Clear(_root);
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(_root).StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source fixture cleanup escaped temporary parent.");
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
