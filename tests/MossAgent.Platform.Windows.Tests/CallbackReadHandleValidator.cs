using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackReadHandleValidator(Action callback) : IFileReadHandleValidator
{
    private int _called;
    public void Validate(FileStream stream, IReadOnlyList<string> authorizedRoots)
    {
        new WindowsFileReadHandleValidator().Validate(stream, authorizedRoots);
        if (Interlocked.Exchange(ref _called, 1) == 0) callback();
    }
}
