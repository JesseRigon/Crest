using Crest.ContentManagement;
using YesSql.Filters.Query;

namespace Crest.Contents.Services;

public sealed class DefaultContentsAdminListFilterParser : IContentsAdminListFilterParser
{
    private readonly IQueryParser<ContentItem> _parser;

    public DefaultContentsAdminListFilterParser(IQueryParser<ContentItem> parser)
    {
        _parser = parser;
    }

    public QueryFilterResult<ContentItem> Parse(string text)
        => _parser.Parse(text);
}
