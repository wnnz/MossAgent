namespace MossAgent.Tools.Abstractions.Files;

public interface IFileRevealService
{
    Task RevealAsync(string path, Action validatePath, CancellationToken cancellationToken);
}
