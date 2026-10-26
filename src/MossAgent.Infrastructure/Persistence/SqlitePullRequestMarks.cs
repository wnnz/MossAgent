using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqlitePullRequestMarks(SqliteConnectionFactory connections,
    TimeProvider? timeProvider = null) : IPullRequestMarks
{
    public const int MaximumNumbers = 64;

    public Task<IReadOnlyList<PullRequestMark>> GetAsync(Guid accountId, string owner, string repository,
        IReadOnlyList<int> numbers, CancellationToken cancellationToken) =>
        Task.Run(() => RunBoundedAsync(token => GetCoreAsync(accountId, owner, repository, numbers, token),
            cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<PullRequestMark>> GetCoreAsync(Guid accountId, string owner, string repository,
        IReadOnlyList<int> numbers, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(accountId, owner, repository);
        if (numbers.Count > MaximumNumbers || numbers.Any(number => number <= 0))
            throw new SourceReviewException("invalid_arguments");
        if (numbers.Count == 0) return [];
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var slots = string.Join(",", Enumerable.Range(0, numbers.Count).Select(index => $"$n{index}"));
        command.CommandText = "SELECT number,head_revision,viewed_at FROM pull_request_marks "
            + "WHERE account_id=$account AND owner=$owner AND repository=$repository "
            + $"AND number IN ({slots}) ORDER BY number;";
        command.Parameters.AddWithValue("$account", accountId.ToString("D"));
        command.Parameters.AddWithValue("$owner", owner);
        command.Parameters.AddWithValue("$repository", repository);
        for (var index = 0; index < numbers.Count; index++)
            command.Parameters.AddWithValue($"$n{index}", numbers[index]);
        var marks = new List<PullRequestMark>(numbers.Count);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var number = reader.GetInt32(0);
            var head = reader.GetString(1);
            if (number <= 0 || !PullRequestRevision.IsCommit(head))
                throw new InvalidOperationException("Invalid stored pull request mark.");
            marks.Add(new(accountId, owner, repository, number, head,
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2))));
        }
        return marks;
    }

    public Task MarkViewedAsync(Guid accountId, string owner, string repository, int number,
        string headRevision, CancellationToken cancellationToken) =>
        Task.Run(() => RunBoundedAsync(token => MarkCoreAsync(accountId, owner, repository, number,
            headRevision, token), cancellationToken), cancellationToken);

    private async Task MarkCoreAsync(Guid accountId, string owner, string repository, int number,
        string headRevision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(accountId, owner, repository);
        if (number <= 0 || !PullRequestRevision.IsCommit(headRevision))
            throw new SourceReviewException("invalid_arguments");
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO pull_request_marks(account_id,owner,repository,number,head_revision,viewed_at)
            VALUES($account,$owner,$repository,$number,$head,$viewed)
            ON CONFLICT(account_id,owner,repository,number)
            DO UPDATE SET head_revision=$head,viewed_at=$viewed;
            """;
        command.Parameters.AddWithValue("$account", accountId.ToString("D"));
        command.Parameters.AddWithValue("$owner", owner);
        command.Parameters.AddWithValue("$repository", repository);
        command.Parameters.AddWithValue("$number", number);
        command.Parameters.AddWithValue("$head", headRevision);
        command.Parameters.AddWithValue("$viewed", Now.ToUnixTimeSeconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DateTimeOffset Now => (timeProvider ?? TimeProvider.System).GetUtcNow();

    private static void ValidateScope(Guid accountId, string owner, string repository)
    {
        if (accountId == Guid.Empty || !Matches(owner, true) || !Matches(repository, false))
            throw new SourceReviewException("invalid_arguments");
    }

    private static bool Matches(string value, bool owner)
    {
        try { return SourceRepositoryScope.ValidateSegment(value, owner) == value; }
        catch (ArgumentException) { return false; }
    }

    private async Task<TResult> RunBoundedAsync<TResult>(Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        return await operation(linked.Token).WaitAsync(linked.Token);
    }

    private async Task RunBoundedAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        await operation(linked.Token).WaitAsync(linked.Token);
    }
}
