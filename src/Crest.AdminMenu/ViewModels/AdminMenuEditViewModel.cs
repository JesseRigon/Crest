using System.ComponentModel.DataAnnotations;

namespace Crest.AdminMenu.ViewModels;

public class AdminMenuEditViewModel
{
    public string Id { get; set; }

    [Required]
    public string Name { get; set; }
}
