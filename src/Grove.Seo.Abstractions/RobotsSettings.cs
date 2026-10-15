using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Grove.Seo;

public class RobotsSettings
{
    [DefaultValue(true)]
    [Display(Name = "Add a rule to allow all web robots.", Description = "Adds: User-agent: *", Order = 0)]
    public bool AllowAllAgents { get; set; } = true;

    [DefaultValue(true)]
    [Display(Name = "Add a rule to disallow web robots to the admin routes.", Description = "Adds: Disallow: /{admin prefix}", Order = 1)]
    public bool DisallowAdmin { get; set; } = true;

    [Display(Name = "Additional rules", Description = "Optionally, you can include any additional rules. A physical robots.txt file in the site's web root overrides these settings.", Order = 2)]
    [DataType(DataType.MultilineText)]
    public string AdditionalRules { get; set; }
}
