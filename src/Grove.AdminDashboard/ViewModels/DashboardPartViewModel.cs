using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.AdminDashboard.Models;
using Grove.ContentManagement;

namespace Grove.AdminDashboard.ViewModels;

public class DashboardPartViewModel
{
    public string ContentItemId { get; set; }
    public double Position { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    [BindNever]
    public ContentItem ContentItem { get; set; }

    [BindNever]
    public DashboardPart DashboardPart { get; set; }
}
