using Crest.Data.Documents;

namespace Crest.AdminMenu.Models;

/// <summary>
/// The list of all the AdminMenu stored on the system.
/// </summary>
public class AdminMenuList : Document
{
    public List<AdminMenu> AdminMenu { get; set; } = [];
}
