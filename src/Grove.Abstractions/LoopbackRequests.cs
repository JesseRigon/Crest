using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Grove;

/// <summary>
/// The server's own calls back into itself: server-rendered admin pages and their circuits call
/// the tenant's APIs over HTTP with the user's forwarded cookies. Such a call is part of the
/// request that caused it, so request-level policies (rate limits) that already counted that
/// request must not count its fan-out again. The mark is a per-process secret, so a client
/// cannot claim it, and it is only ever sent to the server's own address.
/// </summary>
public static class LoopbackRequests
{
    public const string HeaderName = "X-Server-Loopback";

    private static readonly byte[] _token = RandomNumberGenerator.GetBytes(32);
    private static readonly string _tokenText = Convert.ToBase64String(_token);

    /// <summary>Marks an outgoing request to this server as a loopback call.</summary>
    public static void Mark(HttpRequestMessage request)
    {
        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, _tokenText);
    }

    /// <summary>Whether the incoming request is one this process marked.</summary>
    public static bool IsLoopback(HttpContext context)
    {
        var values = context.Request.Headers[HeaderName];
        if (values.Count != 1 || string.IsNullOrEmpty(values[0]))
        {
            return false;
        }

        Span<byte> received = stackalloc byte[64];
        return Convert.TryFromBase64String(values[0], received, out var written)
            && written == _token.Length
            && CryptographicOperations.FixedTimeEquals(received[..written], _token);
    }
}
