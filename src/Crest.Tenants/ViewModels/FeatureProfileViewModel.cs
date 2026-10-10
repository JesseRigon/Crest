using System.ComponentModel.DataAnnotations;

namespace Crest.Tenants.ViewModels;

public class FeatureProfileViewModel
{
    [Required]
    public string Id { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string FeatureRules { get; set; }
}
