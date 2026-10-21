namespace MossAgent.Tools.Abstractions.Files;

public interface IAuthorizedDirectoryLease : IDisposable
{
    int CreatedCount { get; }
}
