using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.AdminMenu.Models;

namespace Crest.AdminMenu.ViewModels;

public class AdminNodeEditViewModel
{
    public string AdminMenuId { get; set; }
    public string AdminNodeId { get; set; }
    public string AdminNodeType { get; set; }

    public int Priority { get; set; }
    public string Position { get; set; }

    public dynamic Editor { get; set; }

    [BindNever]
    public AdminNode AdminNode { get; set; }
}
