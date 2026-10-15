using Grove.Data.Documents;

namespace Grove.Contents.AdminMenu.Models;

/// <summary>
/// The list of all the AdminMenu stored on the system.
/// </summary>
public class AdminMenuList : Document
{
    public List<AdminMenu> AdminMenu { get; set; } = [];
}
