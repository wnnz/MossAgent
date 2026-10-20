using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.ViewModels;
using MossAgent.Application.Networking;
using MossAgent.Application.Persistence;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class PullRequestReviewChatFixture(PullRequestInspectionUiFixture inspection, ReviewChatModelTransport transport) : IAsyncDisposable
{
    public PullRequestInspectionUiFixture Inspection { get; } = inspection;
    public ReviewChatModelTransport Transport { get; } = transport;
    public SourceAccountUiFixture Source => Inspection.Source;
    public WorkspaceViewModel Workspace => Source.Main.Workspace;
    public PullRequestsViewModel Page => Inspection.Model;
    public PullRequestChatLaunchViewModel Chat => Page.Chat!;
    public IWorkspaceRepository Tasks => Source.Get<IWorkspaceRepository>();
    public ISourceReviewChats Chats => Source.Get<ISourceReviewChats>();
    public IConversationDraftRepository Drafts => Source.Get<IConversationDraftRepository>();
    public CancellationToken Token => Source.Token;

    public static async Task<PullRequestReviewChatFixture> CreateAsync()
    {
        var transport = new ReviewChatModelTransport();
        var inspection = await PullRequestInspectionUiFixture.CreateAsync(services =>
        {
            services.RemoveAll<IProviderHttpClientFactory>();
            services.AddSingleton<IProviderHttpClientFactory>(transport);
        });
        var fixture = new PullRequestReviewChatFixture(inspection, transport);
        try
        {
            var provider = new AiProvider(Guid.NewGuid(), "Review model", ProviderProtocol.OpenAiResponses,
                new Uri("https://model.invalid/v1/"), "synthetic-model-key", null, true, true, new Dictionary<string, string>());
            await fixture.Source.Configurations.SaveProviderAsync(provider, fixture.Token);
            await fixture.Source.Configurations.SaveModelAsync(new(Guid.NewGuid(), provider.Id, "gpt-5", "Review fixture",
                32768, 1024, "low", null, null, false, false, true, true, "{}"), fixture.Token);
            await fixture.Workspace.LoadAsync();
            fixture.Source.Main.Pages.IsPullRequestsOpen = true;
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task<AgentTask> OpenAsync(bool investigate = false, bool readOnly = false)
    {
        await (readOnly ? Chat.ReadOnlyCommand : investigate ? Chat.InvestigateCommand : Chat.OpenCommand).ExecuteAsync(null);
        await WaitUntilAsync(() => Source.Main.Pages.IsConversationOpen && Workspace.Catalog.SelectedTask is not null);
        await Workspace.Drafts.LoadCurrentAsync();
        await WaitUntilAsync(() => Workspace.ModelPicker.CanSendWithSelectedModel && Workspace.Drafts.CanSend);
        return Workspace.Catalog.SelectedTask!;
    }

    public async Task ReturnToPullRequestAsync()
    {
        Source.Main.Pages.IsPullRequestsOpen = true;
        await Page.OpenAsync();
        await Page.LoadCommand.ExecuteAsync(null);
        Page.SelectedItem = Page.Items[0];
        await Page.DetailCommand.ExecuteAsync(null);
        Assert.NotNull(Page.Detail);
    }

    public async Task SendAsync(string prompt)
    {
        var requests = Transport.Requests.Count;
        Workspace.ComposerText = prompt;
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
        await WaitUntilAsync(() => Transport.Requests.Count == requests + 1);
        await WaitUntilAsync(() => !Workspace.IsRunning && Workspace.Catalog.SelectedTask?.Status == AgentTaskStatus.Completed);
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try { while (!predicate()) await Task.Delay(10, deadline.Token); }
        catch (OperationCanceledException) when (!Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Review fixture wait: requests={Transport.Requests.Count}, task={Workspace.Catalog.SelectedTask?.Status}, running={Workspace.IsRunning}, activity={Workspace.Activity}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Source.Main.Workspace.ShutdownAsync(); }
        finally { await Inspection.DisposeAsync(); Transport.Dispose(); }
    }
}
