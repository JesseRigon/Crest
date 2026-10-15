using Grove.ContentManagement;
using YesSql.Filters.Query;

namespace Grove.Contents.Services;

public interface IContentsAdminListFilterParser : IQueryParser<ContentItem>;
