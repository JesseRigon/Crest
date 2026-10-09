using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.Alias;
using Crest.Alias.Services;
using Crest.ContentManagement;
using YesSql;

#pragma warning disable CA1050 // Declare types in namespaces
public static class AliasPartRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Returns a content item id by its alias.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="alias">The alias.</param>
    /// <example>GetContentItemIdByAliasAsync("carousel").</example>
    /// <returns>A content item id or <c>null</c> if it was not found.</returns>
    public static async Task<string> GetContentItemIdByAliasAsync(this IPlatformHelper platformHelper, string alias)
    {
        if (string.IsNullOrEmpty(alias))
        {
            return null;
        }

        // Provided for backwards compatibility and avoiding confusion.
        if (alias.StartsWith(AliasConstants.AliasPrefix, StringComparison.OrdinalIgnoreCase))
        {
            alias = alias[AliasConstants.AliasPrefix.Length..];
        }

        var session = platformHelper.HttpContext.RequestServices.GetService<ISession>();
        var aliasPartIndex = await AliasPartContentHandleHelper.QueryAliasIndex(session, alias);

        return aliasPartIndex?.ContentItemId;
    }

    /// <summary>
    /// Loads a content item by its alias.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="alias">The alias to load.</param>
    /// <param name="latest">Whether a draft should be loaded if available. <c>false</c> by default.</param>
    /// <example>GetContentItemIdByAliasAsync("carousel").</example>
    /// <returns>A content item with the specific name, or <c>null</c> if it doesn't exist.</returns>
    public static async Task<ContentItem> GetContentItemByAliasAsync(this IPlatformHelper platformHelper, string alias, bool latest = false)
    {
        var contentItemId = await platformHelper.GetContentItemIdByAliasAsync(alias);
        var contentManager = platformHelper.HttpContext.RequestServices.GetService<IContentManager>();

        return await contentManager.GetAsync(contentItemId, latest ? VersionOptions.Latest : VersionOptions.Published);
    }
}
