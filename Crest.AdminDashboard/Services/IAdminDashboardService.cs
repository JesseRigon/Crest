using System.Linq.Expressions;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;

namespace Crest.AdminDashboard.Services;

/// <summary>
/// Provides services to manage the Admin Dashboards.
/// </summary>
public interface IAdminDashboardService
{
    Task<IEnumerable<ContentItem>> GetWidgetsAsync(Expression<Func<ContentItemIndex, bool>> predicate);
}
