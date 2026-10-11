using Crest.ContentManagement;
using Crest.Contents.ViewModels;
using Crest.DisplayManagement.ModelBinding;
using YesSql;

namespace Crest.Contents.Services;

/// <summary>
/// Provides custom filters to the content items admin listing.
/// <see cref="IContentsAdminListFilterProvider"/> is the preferred way to extend the contents list.
/// This abstraction remains to support backwards compatibility, and alternate extension points.
/// </summary>
public interface IContentsAdminListFilter
{
    Task FilterAsync(ContentOptionsViewModel model, IQuery<ContentItem> query, IUpdateModel updater);
}
