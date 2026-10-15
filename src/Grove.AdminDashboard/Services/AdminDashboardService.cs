using System.Linq.Expressions;
using Grove.AdminDashboard.Indexes;
using Grove.ContentManagement;
using Grove.ContentManagement.Records;
using YesSql;

namespace Grove.AdminDashboard.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly ISession _session;

    public AdminDashboardService(ISession session)
    {
        _session = session;
    }

    public async Task<IEnumerable<ContentItem>> GetWidgetsAsync(Expression<Func<ContentItemIndex, bool>> predicate)
    {
        var widgets = await _session
            .Query<ContentItem, DashboardPartIndex>()
            .OrderBy(w => w.Position)
            .With(predicate)
            .ListAsync();

        return widgets;
    }
}
