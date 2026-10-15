using Grove.ContentManagement;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Views;

namespace Grove.AdminDashboard.ViewModels;

public class DashboardWrapper : ShapeViewModel
{
    public DashboardWrapper() : base("Dashboard_Wrapper")
    {
    }

    public ContentItem Dashboard { get; set; }
    public IShape Content { get; set; }
}
