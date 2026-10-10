using Crest.AuditTrail.Models;
using YesSql.Filters.Query;

namespace Crest.AuditTrail.Services;

public interface IAuditTrailAdminListFilterProvider
{
    void Build(QueryEngineBuilder<AuditTrailEvent> builder);
}
