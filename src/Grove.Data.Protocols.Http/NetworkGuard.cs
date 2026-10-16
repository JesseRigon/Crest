using System.Net;
using System.Net.Sockets;
using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Http;

/// <summary>
/// Which addresses an outgoing call may reach. Checked on the addresses DNS returned for the
/// host actually connected to, so a public name that resolves (or later re-resolves) to a
/// private address is refused as well.
/// </summary>
public static class NetworkGuard
{
    /// <summary>False for loopback, private, link-local, CGNAT, unique-local, multicast and reserved addresses.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // carrier-grade NAT
                || (b[0] == 169 && b[1] == 254)                 // link-local, cloud metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))  // benchmarking
                || b[0] >= 224);                                // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC);                      // unique local fc00::/7
        }

        return false;
    }

    /// <summary>Resolves <paramref name="host"/> and refuses it unless every address is public (or private networks are allowed).</summary>
    public static async Task<IPAddress[]> ResolveAsync(string host, bool allowPrivate, CancellationToken cancellationToken = default)
    {
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new ConnectorRefusedException($"'{host}' does not resolve.");
        }

        if (!allowPrivate && addresses.Any(a => !IsPublic(a)))
        {
            throw new ConnectorRefusedException($"'{host}' resolves to a non-public address; connections may only reach public services.");
        }

        return addresses;
    }

    /// <summary>
    /// A handler that connects only to public addresses (unless <paramref name="allowPrivate"/>)
    /// and follows no redirect. Every connection is checked on the addresses DNS returned.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(bool allowPrivate) => CreateHandler(allowPrivate, null);

    /// <summary>
    /// The guarded handler, pinned to <paramref name="certificateFingerprints"/> when given
    /// (SHA-256 of the leaf certificate, hex): the server is accepted only when its leaf
    /// certificate matches one of them, whether or not its chain is trusted, and refused otherwise.
    /// </summary>
    public static SocketsHttpHandler CreateHandler(bool allowPrivate, IReadOnlyCollection<string>? certificateFingerprints)
    {
        var handler = CreateGuardedHandler(allowPrivate);
        if (certificateFingerprints is { Count: > 0 } pins)
        {
            var expected = pins.Select(NormalizeFingerprint).Where(p => p.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                certificate is not null && expected.Contains(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(certificate.GetRawCertData())));
        }

        return handler;
    }

    /// <summary>A fingerprint as hex without separators ("AB:CD:..." and "abcd..." alike).</summary>
    public static string NormalizeFingerprint(string fingerprint) =>
        new string(fingerprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

    /// <summary>The fingerprints a comma-separated setting names.</summary>
    public static IReadOnlyList<string> ParseFingerprints(string? setting) =>
        string.IsNullOrWhiteSpace(setting) ? [] : setting.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(NormalizeFingerprint).Where(f => f.Length == 64).Distinct().ToArray();

    private static SocketsHttpHandler CreateGuardedHandler(bool allowPrivate) => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await ResolveAsync(context.DnsEndPoint.Host, allowPrivate, cancellationToken);
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };
}

/// <summary>
/// A shell's guarded HTTP client: pooled, no redirects (a redirect could point a credentialed
/// call at another host), every connection vetted by <see cref="NetworkGuard"/>, no client
/// timeout (the engine times each attempt). Owned by the tenant's container, disposed with it.
/// </summary>
public sealed class GuardedHttpClient : IDisposable
{
    public GuardedHttpClient(bool allowPrivateNetworks, IReadOnlyCollection<string>? certificateFingerprints = null)
    {
        AllowPrivateNetworks = allowPrivateNetworks;
        Handler = NetworkGuard.CreateHandler(allowPrivateNetworks, certificateFingerprints);
        Client = new HttpClient(Handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public bool AllowPrivateNetworks { get; }

    public HttpMessageHandler Handler { get; }

    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        Handler.Dispose();
    }
}
