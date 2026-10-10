using Crest.AuditTrail.Models;
using YesSql.Filters.Query;

namespace Crest.AuditTrail.Services;

public interface IAuditTrailAdminListFilterParser : IQueryParser<AuditTrailEvent>;
