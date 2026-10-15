using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Tools.Tests;

internal sealed class ProbeFileRevealService : IFileRevealService
{
    public List<string> Paths { get; } = [];
    public Func<string, CancellationToken, Task> Reveal { get; set; } = (_, _) => Task.CompletedTask;
    public Func<Task>? BeforeValidation { get; set; }
    public async Task RevealAsync(string path, Action validatePath, CancellationToken cancellationToken)
    {
        if (BeforeValidation is not null) await BeforeValidation();
        validatePath();
        Paths.Add(path);
        await Reveal(path, cancellationToken);
    }
}
