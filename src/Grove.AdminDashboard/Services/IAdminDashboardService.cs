using System.Linq.Expressions;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;

namespace Grove.AdminDashboard.Services;

/// <summary>
/// Provides services to manage the Admin Dashboards.
/// </summary>
public interface IAdminDashboardService
{
    Task<IEnumerable<ContentItem>> GetWidgetsAsync(Expression<Func<ContentItemIndex, bool>> predicate);
}
