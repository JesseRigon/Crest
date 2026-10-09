using System.ComponentModel.DataAnnotations;

namespace Crest.AdminMenu.ViewModels;

public class AdminMenuCreateViewModel
{
    [Required]
    public string Name { get; set; }
}
