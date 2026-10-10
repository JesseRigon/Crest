using Crest.ContentManagement;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Views;

namespace Crest.AdminDashboard.ViewModels;

public class DashboardWrapper : ShapeViewModel
{
    public DashboardWrapper() : base("Dashboard_Wrapper")
    {
    }

    public ContentItem Dashboard { get; set; }
    public IShape Content { get; set; }
}
