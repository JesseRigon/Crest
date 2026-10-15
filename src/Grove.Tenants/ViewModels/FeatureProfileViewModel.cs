using System.ComponentModel.DataAnnotations;

namespace Grove.Tenants.ViewModels;

public class FeatureProfileViewModel
{
    [Required]
    public string Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string FeatureRules { get; set; }
}
