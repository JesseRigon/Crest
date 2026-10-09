using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.AdminDashboard.Models;
using Crest.ContentManagement;

namespace Crest.AdminDashboard.ViewModels;

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
