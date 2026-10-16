using Grove.Contents.Alias.Indexes;
using Grove.Contents.Management;
using YesSql;

namespace Grove.Contents.Alias.Services;

public class AliasFieldSetContentHandleProvider : IContentHandleProvider
{
    private readonly ISession _session;

    public AliasFieldSetContentHandleProvider(ISession session)
    {
        _session = session;
    }

    public int Order => 100;

    public async Task<string> GetContentItemIdAsync(string handle)
    {
        if (handle.StartsWith(AliasConstants.AliasPrefix, StringComparison.OrdinalIgnoreCase))
        {
            handle = handle[AliasConstants.AliasPrefix.Length..];

            var aliasIndex = await AliasFieldSetContentHandleHelper.QueryAliasIndex(_session, handle);
            return aliasIndex?.ContentItemId;
        }

        return null;
    }
}

internal sealed class AliasFieldSetContentHandleHelper
{
#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
    internal static Task<ContentItem> QueryAliasIndex(ISession session, string alias) =>
        session.Query<ContentItem, AliasIndex>(x => x.Alias == alias.ToLowerInvariant()).FirstOrDefaultAsync();
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
}
