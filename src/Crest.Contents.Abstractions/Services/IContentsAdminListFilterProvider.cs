using Crest.ContentManagement;
using YesSql.Filters.Query;

namespace Crest.Contents.Services;

/// <summary>
/// Provides a custom parsing engine rule set for filtering content items in the contents admin list.
/// </summary>
public interface IContentsAdminListFilterProvider
{
    void Build(QueryEngineBuilder<ContentItem> builder);
}
