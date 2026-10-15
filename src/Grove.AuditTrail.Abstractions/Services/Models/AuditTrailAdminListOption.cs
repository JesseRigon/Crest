using Microsoft.AspNetCore.Mvc.Rendering;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.ViewModels;
using YesSql;
using YesSql.Filters.Query.Services;

namespace Grove.AuditTrail.Services.Models;

public class AuditTrailAdminListOption
{
    public AuditTrailAdminListOption(
        string value,
        Func<string, IQuery<AuditTrailEvent>, QueryExecutionContext<AuditTrailEvent>, ValueTask<IQuery<AuditTrailEvent>>> query,
        Func<IServiceProvider, AuditTrailAdminListOption, AuditTrailIndexOptions, SelectListItem> selectListItem,
        bool isDefault)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(query);

        Value = value;
        Query = query;
        SelectListItem = selectListItem;
        IsDefault = isDefault;
    }

    public string Value { get; }
    public Func<string, IQuery<AuditTrailEvent>, QueryExecutionContext<AuditTrailEvent>, ValueTask<IQuery<AuditTrailEvent>>> Query { get; }
    public Func<IServiceProvider, AuditTrailAdminListOption, AuditTrailIndexOptions, SelectListItem> SelectListItem { get; }
    public bool IsDefault { get; }
}
