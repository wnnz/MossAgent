using System.Collections.Concurrent;
using System.Net;
using System.Text;
using MossAgent.Application.Networking;
using MossAgent.Domain;

namespace MossAgent.App.Tests;

internal sealed class ReviewChatModelTransport : HttpMessageHandler, IProviderHttpClientFactory
{
    public ConcurrentQueue<string> Requests { get; } = new();
    public Func<CancellationToken, Task<HttpResponseMessage>>? RespondAsync { get; set; }
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<HttpClient> CreateAsync(AiProvider provider, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpClient(this, false) { BaseAddress = provider.BaseUri });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(await request.Content!.ReadAsStringAsync(cancellationToken)); Started.TrySetResult();
        if (RespondAsync is { } respond) return await respond(cancellationToken);
        return Reply();
    }

    public static HttpResponseMessage Reply() => new(HttpStatusCode.OK) { Content = new StringContent("""
            data: {"type":"response.output_text.delta","delta":"snapshot reply"}

            data: {"type":"response.completed","response":{"id":"review-fixture"}}

            data: [DONE]

            """, Encoding.UTF8, "text/event-stream") };
}
