using System.ComponentModel.DataAnnotations;
using Crest.AdminMenu.Models;

namespace Crest.AdminMenu.AdminNodes;

public class LinkAdminNode : AdminNode
{
    [Required]
    public string LinkText { get; set; }

    [Required]
    public string LinkUrl { get; set; }

    public string IconClass { get; set; }

    /// <summary>
    /// The names of the permissions required to view this admin menu node.
    /// </summary>
    public string[] PermissionNames { get; set; } = [];
}
