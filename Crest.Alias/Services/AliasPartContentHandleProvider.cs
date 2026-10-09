using Crest.Alias.Indexes;
using Crest.ContentManagement;
using YesSql;

namespace Crest.Alias.Services;

public class AliasPartContentHandleProvider : IContentHandleProvider
{
    private readonly ISession _session;

    public AliasPartContentHandleProvider(ISession session)
    {
        _session = session;
    }

    public int Order => 100;

    public async Task<string> GetContentItemIdAsync(string handle)
    {
        if (handle.StartsWith(AliasConstants.AliasPrefix, StringComparison.OrdinalIgnoreCase))
        {
            handle = handle[AliasConstants.AliasPrefix.Length..];

            var aliasPartIndex = await AliasPartContentHandleHelper.QueryAliasIndex(_session, handle);
            return aliasPartIndex?.ContentItemId;
        }

        return null;
    }
}

internal sealed class AliasPartContentHandleHelper
{
#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
    internal static Task<ContentItem> QueryAliasIndex(ISession session, string alias) =>
        session.Query<ContentItem, AliasPartIndex>(x => x.Alias == alias.ToLowerInvariant()).FirstOrDefaultAsync();
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
}
