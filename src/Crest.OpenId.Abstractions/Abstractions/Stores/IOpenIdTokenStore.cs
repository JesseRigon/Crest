using OpenIddict.Abstractions;

namespace Crest.OpenId.Abstractions.Stores;

public interface IOpenIdTokenStore<TToken> : IOpenIddictTokenStore<TToken> where TToken : class
{
    ValueTask<TToken> FindByPhysicalIdAsync(string identifier, CancellationToken cancellationToken);
    ValueTask<string> GetPhysicalIdAsync(TToken token, CancellationToken cancellationToken);
}
