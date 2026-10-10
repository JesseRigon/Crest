using Crest.Users.Models;
using YesSql.Filters.Query;

namespace Crest.Users.Services;

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
