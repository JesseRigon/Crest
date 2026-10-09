using YesSql.Filters.Query;

namespace Crest.Notifications.Services;

public sealed class DefaultNotificationAdminListFilterParser : INotificationAdminListFilterParser
{
    private readonly IQueryParser<Notification> _parser;

    public DefaultNotificationAdminListFilterParser(IQueryParser<Notification> parser)
    {
        _parser = parser;
    }

    public QueryFilterResult<Notification> Parse(string text)
        => _parser.Parse(text);
}
