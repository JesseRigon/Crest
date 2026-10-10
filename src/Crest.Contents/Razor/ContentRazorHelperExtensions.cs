using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using YesSql;

namespace Crest;

[Obsolete("This class has been deprecated, please use ContentPlatformHelperExtensions instead.")]
public static class ContentRazorHelperExtensions
{
    /// <summary>
    /// Returns a content item id by its handle.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="handle">The handle.</param>
    /// <example>GetContentItemIdByHandleAsync("alias:carousel").</example>
    /// <example>GetContentItemIdByHandleAsync("slug:myblog/my-blog-post").</example>
    /// <returns>A content item id or <c>null</c> if it was not found.</returns>
    public static Task<string> GetContentItemIdByHandleAsync(this IPlatformHelper platformHelper, string handle)
        => ContentPlatformHelperExtensions.GetContentItemIdByHandleAsync(platformHelper, handle);

    /// <summary>
    /// Loads a content item by its handle.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="handle">The handle to load.</param>
    /// <param name="option">A specific version to load or the default version.</param>
    /// <returns>A content item with the specific name, or <c>null</c> if it doesn't exist.</returns>
    public static async Task<ContentItem> GetContentItemByHandleAsync(this IPlatformHelper platformHelper, string handle, VersionOptions option = null)
        => await ContentPlatformHelperExtensions.GetContentItemByHandleAsync(platformHelper, handle, option);

    /// <summary>
    /// Loads a content item by its id.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="contentItemId">The content item id to load.</param>
    /// <param name="option">A specific version to load or the default version.</param>
    /// <returns>A content item with the specific id, or <c>null</c> if it doesn't exist.</returns>
    public static Task<ContentItem> GetContentItemByIdAsync(this IPlatformHelper platformHelper, string contentItemId, VersionOptions option = null)
        => ContentPlatformHelperExtensions.GetContentItemByIdAsync(platformHelper, contentItemId, option);

    /// <summary>
    /// Loads a list of content items by their ids.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="contentItemIds">The content item ids to load.</param>
    /// <param name="option">A specific version to load or the default version.</param>
    /// <returns>A list of content items with the specific ids.</returns>
    public static Task<IEnumerable<ContentItem>> GetContentItemsByIdAsync(this IPlatformHelper platformHelper, IEnumerable<string> contentItemIds, VersionOptions option = null)
        => ContentPlatformHelperExtensions.GetContentItemsByIdAsync(platformHelper, contentItemIds, option);

    /// <summary>
    /// Loads a content item by its version id.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="contentItemVersionId">The content item version id to load.</param>
    /// <returns>A content item with the specific version id, or <c>null</c> if it doesn't exist.</returns>
    public static Task<ContentItem> GetContentItemByVersionIdAsync(this IPlatformHelper platformHelper, string contentItemVersionId)
        => ContentPlatformHelperExtensions.GetContentItemByVersionIdAsync(platformHelper, contentItemVersionId);

    /// <summary>
    /// Query content items.
    /// </summary>
    public static async Task<IEnumerable<ContentItem>> QueryContentItemsAsync(this IPlatformHelper platformHelper, Func<IQuery<ContentItem, ContentItemIndex>, IQuery<ContentItem>> query)
        => await ContentPlatformHelperExtensions.QueryContentItemsAsync(platformHelper, query);

    /// <summary>
    /// Loads content items of a specific type.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="contentType">The content type to load.</param>
    /// <param name="maxContentItems">The maximum content items to return.</param>
    public static Task<IEnumerable<ContentItem>> GetRecentContentItemsByContentTypeAsync(this IPlatformHelper platformHelper, string contentType, int maxContentItems = 10)
        => ContentPlatformHelperExtensions.GetRecentContentItemsByContentTypeAsync(platformHelper, contentType, maxContentItems);
}
