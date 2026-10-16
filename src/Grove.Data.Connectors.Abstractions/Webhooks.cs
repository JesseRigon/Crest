namespace Grove.Data.Connectors;

/// <summary>An inbound request to verify: the raw body and the headers as received.</summary>
public sealed record WebhookRequest(ReadOnlyMemory<byte> Body, IReadOnlyDictionary<string, string> Headers);

public enum WebhookVerificationStatus
{
    Verified,

    /// <summary>No such connection: answer 404.</summary>
    UnknownConnection,

    /// <summary>The connection is not an inbound (hmac) connection: answer 404.</summary>
    NotInbound,

    /// <summary>Missing or wrong signature, or no secret stored: answer 401.</summary>
    Rejected,
}

public sealed record WebhookVerification(WebhookVerificationStatus Status, ConnectionDefinition? Connection = null, string? Error = null)
{
    public bool Verified => Status == WebhookVerificationStatus.Verified;
}

/// <summary>
/// Verifies an inbound webhook's signature for a connection. The engine reads the signing
/// secret (tenant scope) and hands it to the scheme; the caller never sees it.
/// </summary>
public interface IWebhookVerifier
{
    Task<WebhookVerification> VerifyAsync(string connectionKey, WebhookRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A signature scheme (<c>Grove.Data.Protocols.Webhooks</c>): HMAC-SHA256 hex, a vendor's variant, ...</summary>
public interface IWebhookSignatureScheme
{
    /// <summary><see cref="ConnectionAuth.SignatureScheme"/>; <c>hmac-sha256</c> is the default.</summary>
    string Id { get; }

    /// <summary>Constant-time check of the request's signature with <paramref name="secret"/>.</summary>
    bool Verify(ReadOnlySpan<byte> secret, WebhookRequest request, string? signatureHeader);
}
