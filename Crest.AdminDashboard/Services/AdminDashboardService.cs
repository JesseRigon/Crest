using System.Linq.Expressions;
using Crest.AdminDashboard.Indexes;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using YesSql;

namespace Crest.AdminDashboard.Services;

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
