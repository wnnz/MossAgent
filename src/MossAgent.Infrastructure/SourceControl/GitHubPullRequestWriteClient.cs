using System.Text.Json;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubPullRequestWriteClient(ISourceAccountHttpClientFactory clients) : IPullRequestWriteClient
{
    private const int MaximumBodyLength = 4096;
    private readonly GitHubSourceTransport _transport = new(clients);

    public async Task<long> CommentAsync(SourceAccount account, SourceRepositoryScope scope, PullRequestRevision revision,
        string body, CancellationToken cancellationToken)
    {
        revision.Validate();
        Validate(account, scope);
        if (string.IsNullOrEmpty(body) || body.Length > MaximumBodyLength) throw new SourceReviewException("invalid_arguments");
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { body });
        var response = await _transport.PostAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/issues/{revision.Number}/comments", payload, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
            var item = document.RootElement;
            if (item.ValueKind != JsonValueKind.Object) throw GitHubInspectionJson.Invalid();
            var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/issues/{revision.Number}");
            GitHubInspectionJson.CheckOwner(item, "issue_url", expected, account.AccessToken);
            return GitHubInspectionJson.Id(item);
        }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    public async Task<long?> FindCommentAsync(SourceAccount account, SourceRepositoryScope scope, int number,
        string body, CancellationToken cancellationToken)
    {
        Validate(account, scope);
        if (number <= 0 || string.IsNullOrEmpty(body) || body.Length > MaximumBodyLength)
            throw new SourceReviewException("invalid_arguments");
        var response = await _transport.GetAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/issues/{number}/comments?per_page=5&sort=created&direction=desc",
            cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
            var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/issues/{number}");
            foreach (var item in GitHubInspectionJson.Items(document.RootElement))
            {
                GitHubInspectionJson.CheckOwner(item, "issue_url", expected, account.AccessToken);
                if (GitHubInspectionJson.OptionalText(item, "body", 16384, account.AccessToken, allowEmpty: true) == body)
                    return GitHubInspectionJson.Id(item);
            }
            return null;
        }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    // commit_id pins the review to the captured head revision so the server rejects the write
    // when the pull request head moved, instead of silently reviewing a newer commit.
    public async Task<long> SubmitReviewAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken)
    {
        revision.Validate();
        Validate(account, scope);
        if (body is { Length: > MaximumBodyLength }) throw new SourceReviewException("invalid_arguments");
        var payload = new Dictionary<string, string>
        { ["commit_id"] = revision.HeadRevision, ["event"] = EventName(decision) };
        if (!string.IsNullOrEmpty(body)) payload["body"] = body;
        var response = await _transport.PostAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}/reviews",
            JsonSerializer.SerializeToUtf8Bytes(payload), cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
            var item = document.RootElement;
            if (item.ValueKind != JsonValueKind.Object) throw GitHubInspectionJson.Invalid();
            var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}");
            GitHubInspectionJson.CheckOwner(item, "pull_request_url", expected, account.AccessToken);
            return GitHubInspectionJson.Id(item);
        }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    // Reconcile compares state, head commit and body. An absent body is sent as no body field,
    // and the server echoes it as null or "", so both sides normalize to "" before matching.
    // The reviews endpoint returns chronologically ascending pages with no sort option, so
    // the just-submitted review sits on the last page: walk bounded pages until a short page.
    public async Task<long?> FindReviewAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, PullRequestReviewDecision decision, string? body, CancellationToken cancellationToken)
    {
        revision.Validate();
        Validate(account, scope);
        if (body is { Length: > MaximumBodyLength }) throw new SourceReviewException("invalid_arguments");
        var expectedState = StateName(decision);
        var expectedBody = body ?? string.Empty;
        var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}");
        for (var page = 1; page <= 10; page++)
        {
            var response = await _transport.GetAsync(account,
                $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}/reviews?per_page=20&page={page}", cancellationToken);
            long? found;
            int count;
            try { (found, count) = Scan(response, expected, expectedState, revision.HeadRevision, expectedBody, account.AccessToken); }
            catch (SourceReviewException) { throw; }
            catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
            if (found is not null || count < 20) return found;
        }
        return null;
    }

    // Close is a single PATCH with a fixed {"state":"closed"} body; the response must
    // belong to the exact repository and pull request, and the returned state is handed
    // to the writer which treats anything but "closed" as an invalid response.
    public async Task<string> CloseAsync(SourceAccount account, SourceRepositoryScope scope,
        PullRequestRevision revision, CancellationToken cancellationToken)
    {
        revision.Validate();
        Validate(account, scope);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { state = "closed" });
        var response = await _transport.PatchAsync(account,
            $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}", payload, cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
            var item = document.RootElement;
            if (item.ValueKind != JsonValueKind.Object) throw GitHubInspectionJson.Invalid();
            var expected = new Uri(account.ApiEndpoint, $"repos/{scope.Owner}/{scope.Repository}/pulls/{revision.Number}");
            GitHubInspectionJson.CheckOwner(item, "url", expected, account.AccessToken);
            if (!item.GetProperty("number").TryGetInt32(out var number) || number != revision.Number)
                throw GitHubInspectionJson.Invalid();
            return GitHubInspectionJson.Text(item, "state", 32, account.AccessToken);
        }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_invalid_response"); }
    }

    private static (long? Found, int Count) Scan(byte[] response, Uri expected, string expectedState,
        string expectedCommit, string expectedBody, string token)
    {
        using var document = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 24 });
        var items = GitHubInspectionJson.Items(document.RootElement);
        foreach (var item in items)
        {
            GitHubInspectionJson.CheckOwner(item, "pull_request_url", expected, token);
            if (GitHubInspectionJson.OptionalText(item, "state", 32, token) != expectedState) continue;
            var commit = GitHubInspectionJson.OptionalText(item, "commit_id", 64, token);
            if (commit is null || !commit.Equals(expectedCommit, StringComparison.OrdinalIgnoreCase)) continue;
            var found = GitHubInspectionJson.OptionalText(item, "body", 16384, token, allowEmpty: true);
            if ((found ?? string.Empty) != expectedBody) continue;
            return (GitHubInspectionJson.Id(item), items.Length);
        }
        return (null, items.Length);
    }

    private static string EventName(PullRequestReviewDecision decision) => decision switch
    {
        PullRequestReviewDecision.Approve => "APPROVE",
        PullRequestReviewDecision.RequestChanges => "REQUEST_CHANGES",
        PullRequestReviewDecision.Comment => "COMMENT",
        _ => throw new SourceReviewException("invalid_arguments")
    };

    private static string StateName(PullRequestReviewDecision decision) => decision switch
    {
        PullRequestReviewDecision.Approve => "APPROVED",
        PullRequestReviewDecision.RequestChanges => "CHANGES_REQUESTED",
        PullRequestReviewDecision.Comment => "COMMENTED",
        _ => throw new SourceReviewException("invalid_arguments")
    };

    private static void Validate(SourceAccount account, SourceRepositoryScope scope)
    {
        if (!account.IsEnabled || !scope.CanWrite || account.Id != scope.AccountId || account.Revision != scope.AccountRevision)
            throw new SourceReviewException("source_scope_denied");
        if (SourceRepositoryScope.ValidateSegment(scope.Owner, true) != scope.Owner
            || SourceRepositoryScope.ValidateSegment(scope.Repository) != scope.Repository)
            throw new SourceReviewException("invalid_arguments");
    }
}
