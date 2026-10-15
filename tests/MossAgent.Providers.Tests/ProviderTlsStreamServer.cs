using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MossAgent.Providers.Tests;

internal sealed class ProviderTlsStreamServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stopping;
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _serving;
    private readonly bool _expectCertificateRejection;

    public ProviderTlsStreamServer(string prefix, string tail, CancellationToken token, bool expectCertificateRejection = false)
    {
        _expectCertificateRejection = expectCertificateRejection;
        Certificate = CreateCertificate();
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener.Start();
        BaseUri = new Uri($"https://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/v1/");
        _serving = ServeAsync(Encoding.UTF8.GetBytes(prefix), Encoding.UTF8.GetBytes(tail), _stopping.Token);
    }

    public X509Certificate2 Certificate { get; }
    public Uri BaseUri { get; }
    public Task Completion => _serving;
    public TaskCompletionSource PrefixWritten { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Disconnected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CertificateRejected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool TailReleased => _release.Task.IsCompleted;
    public void ReleaseTail() => _release.TrySetResult();

    public SocketsHttpHandler CreateHandler(X509Certificate2? pinnedCertificate = null)
    {
        var expected = (pinnedCertificate ?? Certificate).RawData;
        return new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                    certificate is not null
                    && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
                    && CryptographicOperations.FixedTimeEquals(certificate.GetRawCertData(), expected)
            }
        };
    }

    private async Task ServeAsync(byte[] prefix, byte[] tail, CancellationToken token)
    {
        using var client = await _listener.AcceptTcpClientAsync(token);
        await using var stream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        if (!await ReadAuthenticatedRequestAsync(stream, token)) return;
        var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\n"
            + $"Content-Length: {prefix.Length + tail.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(prefix, token);
        await stream.FlushAsync(token);
        PrefixWritten.TrySetResult();
        var disconnected = ObserveDisconnectAsync(stream, token);
        if (await Task.WhenAny(_release.Task, disconnected).WaitAsync(token) == _release.Task)
        {
            await stream.WriteAsync(tail, token);
            await stream.FlushAsync(token);
        }
        await disconnected;
    }

    private async Task<bool> ReadAuthenticatedRequestAsync(SslStream stream, CancellationToken token)
    {
        try
        {
            await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = Certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, token);
            await ReadRequestAsync(stream, token);
            return true;
        }
        catch (Exception exception) when (_expectCertificateRejection && !token.IsCancellationRequested
            && (exception is AuthenticationException or IOException))
        {
            CertificateRejected.TrySetResult();
            return false;
        }
    }

    private async Task ObserveDisconnectAsync(Stream stream, CancellationToken token)
    {
        try
        {
            if (await stream.ReadAsync(new byte[1], token) != 0)
                throw new InvalidDataException("Unexpected bytes on the held TLS response.");
        }
        catch (IOException) when (!token.IsCancellationRequested) { }
        token.ThrowIfCancellationRequested();
        Disconnected.TrySetResult();
    }

    private static async Task ReadRequestAsync(Stream stream, CancellationToken token)
    {
        using var received = new MemoryStream();
        var buffer = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length <= 1_048_576 + 16_384)
        {
            var count = await stream.ReadAsync(buffer, token);
            if (count == 0) throw new EndOfStreamException();
            received.Write(buffer, 0, count);
            var bytes = received.ToArray();
            if (bodyStart < 0 && bytes.AsSpan().IndexOf("\r\n\r\n"u8) is var headerEnd and >= 0)
            {
                bodyStart = headerEnd + 4;
                if (bodyStart > 16_384) throw new InvalidDataException("TLS fixture header limit reached.");
                var lines = Encoding.ASCII.GetString(bytes, 0, bodyStart).Split("\r\n");
                var length = lines.Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
                if (bodyLength is < 0 or > 1_048_576) throw new InvalidDataException("TLS fixture body limit reached.");
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength) return;
            if (bodyStart < 0 && received.Length > 16_384)
                throw new InvalidDataException("TLS fixture header limit reached.");
        }
        throw new InvalidDataException("TLS fixture request limit reached.");
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), null,
            X509KeyStorageFlags.UserKeySet);
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        _listener.Stop();
        try { await _serving; }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        finally { _stopping.Dispose(); Certificate.Dispose(); }
    }
}
