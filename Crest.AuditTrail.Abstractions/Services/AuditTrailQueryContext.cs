using Crest.AuditTrail.Models;
using YesSql;
using YesSql.Filters.Query.Services;

namespace Crest.AuditTrail.Services;

public class AuditTrailQueryContext : QueryExecutionContext<AuditTrailEvent>
{
    public AuditTrailQueryContext(IServiceProvider serviceProvider, IQuery<AuditTrailEvent> query) : base(query)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}
