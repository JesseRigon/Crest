using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using Grove.Data.Connectors;
using MailKit;
using MailKit.Net.Proxy;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Smtp;

/// <summary>
/// The non-secret settings of a mail server connection (protocols <c>smtp</c>, <c>imap</c> and
/// <c>pop</c>), as the Connections screen stores them on the connection. The credential's
/// <see cref="CredentialKeys.Username"/> and <see cref="CredentialKeys.Password"/> sign in.
/// </summary>
public static class MailConnectionSettings
{
    /// <summary>The server's host name or address (required).</summary>
    public const string Host = "host";

    /// <summary>The port; the protocol's default (SMTP 587, IMAP 993, POP3 995) when absent.</summary>
    public const string Port = "port";

    /// <summary><c>auto</c> (default), <c>none</c>, <c>ssl</c> (TLS on connect) or <c>starttls</c>.</summary>
    public const string Security = "security";

    /// <summary><c>true</c> accepts a server certificate that does not validate (a test server).</summary>
    public const string AcceptInvalidCertificate = "acceptInvalidCertificate";

    /// <summary>A SOCKS5 proxy the connection goes through, and its port.</summary>
    public const string ProxyHost = "proxyHost";

    public const string ProxyPort = "proxyPort";

    /// <summary>The IMAP folder read when the caller names none (<c>INBOX</c> by default).</summary>
    public const string Folder = "folder";
}

/// <summary>Where a mail server connection goes, read from its settings.</summary>
public sealed record MailServerEndpoint(
    string Host,
    int Port,
    SmtpEncryptionMethod Security,
    bool AcceptInvalidCertificate,
    string? ProxyHost,
    int ProxyPort)
{
    /// <summary>Reads the endpoint; a connection without a host is refused before anything is sent.</summary>
    public static MailServerEndpoint From(ConnectionDefinition connection, int defaultPort)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var host = connection.Setting(MailConnectionSettings.Host)
            ?? throw new ConnectorRefusedException($"The connection '{connection.Key}' names no mail server host.");

        var port = int.TryParse(connection.Setting(MailConnectionSettings.Port), out var value) && value is > 0 and <= 65535
            ? value
            : defaultPort;

        var security = connection.Setting(MailConnectionSettings.Security)?.ToLowerInvariant() switch
        {
            "none" => SmtpEncryptionMethod.None,
            "ssl" or "tls" or "ssltls" => SmtpEncryptionMethod.SslTls,
            "starttls" => SmtpEncryptionMethod.StartTls,
            _ => SmtpEncryptionMethod.Auto,
        };

        var proxyHost = connection.Setting(MailConnectionSettings.ProxyHost);
        var proxyPort = int.TryParse(connection.Setting(MailConnectionSettings.ProxyPort), out var proxy) ? proxy : 1080;

        return new MailServerEndpoint(
            host,
            port,
            security,
            bool.TryParse(connection.Setting(MailConnectionSettings.AcceptInvalidCertificate), out var accept) && accept,
            proxyHost,
            proxyPort);
    }

    public SecureSocketOptions SecureSocketOptions => Security switch
    {
        SmtpEncryptionMethod.None => SecureSocketOptions.None,
        SmtpEncryptionMethod.SslTls => SecureSocketOptions.SslOnConnect,
        SmtpEncryptionMethod.StartTls => SecureSocketOptions.StartTls,
        _ => SecureSocketOptions.Auto,
    };
}

/// <summary>
/// Opens a MailKit client (SMTP, IMAP, POP3) on a mail server connection inside one engine
/// attempt: the address guard the HTTP protocols get from the guarded client (no loopback,
/// private, link-local or CGNAT address unless the shell allows private networks), the
/// connection's timeout and certificate rule, and the credential. Also classifies what failed
/// before anything was handed to the server, which is always safe to retry.
/// </summary>
public static class MailServerConnection
{
    public static async Task ConnectAsync(IMailService client, MailServerEndpoint endpoint, IProtocolCallContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(context);

        var allowPrivate = context.Services.GetService<IOptions<ConnectorOptions>>()?.Value.AllowPrivateNetworks == true;

        client.Timeout = (int)Math.Clamp(context.Connection.Policy.Timeout.TotalMilliseconds, 1000, int.MaxValue);
        client.ServerCertificateValidationCallback = (_, _, _, errors) => errors == SslPolicyErrors.None || endpoint.AcceptInvalidCertificate;

        if (!string.IsNullOrEmpty(endpoint.ProxyHost))
        {
            await GuardAsync(endpoint.ProxyHost, allowPrivate, cancellationToken);
            client.ProxyClient = new Socks5Client(endpoint.ProxyHost, endpoint.ProxyPort);
            await client.ConnectAsync(endpoint.Host, endpoint.Port, endpoint.SecureSocketOptions, cancellationToken);
            return;
        }

        // Connect to the address the guard vetted, so a second DNS answer cannot redirect the client.
        var address = await GuardAsync(endpoint.Host, allowPrivate, cancellationToken);
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(address, endpoint.Port, cancellationToken);
            await client.ConnectAsync(socket, endpoint.Host, endpoint.Port, endpoint.SecureSocketOptions, cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Signs in with the credential's user name and password; no credential (auth scheme none) signs in as nobody.</summary>
    public static async Task AuthenticateAsync(IMailService client, ConnectionCredential? credential, CancellationToken cancellationToken)
    {
        var userName = credential?[CredentialKeys.Username];
        if (userName is null)
        {
            return;
        }

        await client.AuthenticateAsync(userName, credential![CredentialKeys.Password] ?? string.Empty, cancellationToken);
    }

    /// <summary>Resolves the host and returns an address the shell may call; refuses a non-public one.</summary>
    public static async Task<IPAddress> GuardAsync(string host, bool allowPrivateNetworks, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        foreach (var address in addresses)
        {
            if (allowPrivateNetworks || !IsNonPublic(address))
            {
                return address;
            }
        }

        throw new ConnectorRefusedException($"The mail server '{host}' resolves to an address this shell may not call.");
    }

    /// <summary>Loopback, unspecified, private, link-local, CGNAT, multicast and unique-local addresses.</summary>
    public static bool IsNonPublic(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 0
                || bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                || bytes[0] >= 224;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal;
    }

    /// <summary>
    /// A failure while connecting or signing in: nothing reached the server's mail transaction,
    /// so every verdict carries <see cref="FailureClassification.NotSent"/>.
    /// </summary>
    public static FailureClassification ClassifyBeforeSend(Exception exception) => exception switch
    {
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        CredentialUnreadableException => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        AuthenticationException => new FailureClassification(FailureKind.Unauthorized, NotSent: true),
        SslHandshakeException => new FailureClassification(FailureKind.Permanent, NotSent: true),
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData } => new FailureClassification(FailureKind.Transient, NotSent: true),
        SocketException or IOException or TimeoutException or ProtocolException or ServiceNotConnectedException
            => new FailureClassification(FailureKind.Transient, NotSent: true),
        CommandException => new FailureClassification(FailureKind.Permanent, NotSent: true),
        _ => new FailureClassification(FailureKind.Permanent, NotSent: true),
    };
}
