using OpenIddict.Abstractions;

namespace Crest.OpenId.Abstractions.Stores;

public interface IOpenIdScopeStore<TScope> : IOpenIddictScopeStore<TScope> where TScope : class
{
    ValueTask<TScope> FindByPhysicalIdAsync(string identifier, CancellationToken cancellationToken);
    ValueTask<string> GetPhysicalIdAsync(TScope scope, CancellationToken cancellationToken);
}
