namespace Grove.Communications.Email;

public interface IEmailReceiveProviderResolver
{
    /// <summary>
    /// The receive provider of the given technical name; null or empty returns the default one
    /// (null when there is none). Throws <see cref="InvalidEmailProviderException"/> for an unknown name.
    /// </summary>
    ValueTask<IEmailReceiveProvider> GetAsync(string providerName = null);
}

/// <summary>Receives mail through the selected receive provider, or the default one.</summary>
public interface IEmailReceiveService
{
    Task<Grove.Infrastructure.Result<EmailReceiveBatch>> ReceiveAsync(EmailReceiveRequest request, string providerName = null, CancellationToken cancellationToken = default);
}
