using System.ComponentModel.DataAnnotations;

namespace Grove.AdminMenu.ViewModels;

public class AdminMenuCreateViewModel
{
    [Required]
    public string Name { get; set; }
}
