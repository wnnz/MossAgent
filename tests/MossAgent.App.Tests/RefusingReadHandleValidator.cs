using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.App.Tests;

internal sealed class RefusingReadHandleValidator : IFileReadHandleValidator
{
    public void Validate(FileStream stream, IReadOnlyList<string> authorizedRoots) =>
        throw new UnauthorizedAccessException("A caller must not replace the production guard.");
}
