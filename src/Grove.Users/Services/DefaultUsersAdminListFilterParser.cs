using Grove.Users.Models;
using YesSql.Filters.Query;

namespace Grove.Users.Services;

public sealed class DefaultUsersAdminListFilterParser : IUsersAdminListFilterParser
{
    private readonly IQueryParser<User> _parser;

    public DefaultUsersAdminListFilterParser(IQueryParser<User> parser)
    {
        _parser = parser;
    }

    public QueryFilterResult<User> Parse(string text)
        => _parser.Parse(text);
}
