using System.ComponentModel.DataAnnotations;

namespace Crest.OpenId.ViewModels;

public class CreateOpenIdScopeViewModel
{
    public string Description { get; set; }

    [Required]
    public string DisplayName { get; set; }

    [Required]
    public string Name { get; set; }

    public string Resources { get; set; }
}
