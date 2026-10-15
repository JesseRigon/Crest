using Grove.AuditTrail.Models;
using YesSql.Filters.Query;

namespace Grove.AuditTrail.Services;

public interface IAuditTrailAdminListFilterParser : IQueryParser<AuditTrailEvent>;
