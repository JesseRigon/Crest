using Grove.AuditTrail.Models;
using YesSql.Filters.Query;

namespace Grove.AuditTrail.Services;

public sealed class DefaultAuditTrailAdminListFilterParser : IAuditTrailAdminListFilterParser
{
    private readonly IQueryParser<AuditTrailEvent> _parser;

    public DefaultAuditTrailAdminListFilterParser(IQueryParser<AuditTrailEvent> parser)
    {
        _parser = parser;
    }

    public QueryFilterResult<AuditTrailEvent> Parse(string text)
        => _parser.Parse(text);
}
