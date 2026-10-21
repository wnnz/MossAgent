using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Tools.BuiltIn.Files;

internal sealed class OwnedArtifactPayloadWriter(IArtifactFileSessionFactory sessions)
{
    public async Task<ToolArtifact> WriteAsync(string name, byte[] bytes, string mediaType, string prefix, string extension,
        ToolExecutionContext context, Func<CancellationToken, Task> validate, CancellationToken token)
    {
        await validate(token);
        using var session = await sessions.CreateAsync(context, prefix, extension, token);
        await session.Stream.WriteAsync(bytes, token);
        await session.CompleteAsync(token);
        await validate(token);
        token.ThrowIfCancellationRequested();
        session.Retain();
        return new(name, session.Path, mediaType, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
