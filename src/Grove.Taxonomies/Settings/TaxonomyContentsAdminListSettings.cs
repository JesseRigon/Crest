using System.ComponentModel.DataAnnotations;
using Grove.Settings;

namespace Grove.Taxonomies.Settings;

public class TaxonomyContentsAdminListSettings
{
    public const string GroupId = "taxonomyContentsAdminList";

    [Display(Name = "Taxonomies", Description = "The taxonomies to filters in the contents list.")]
    [Setting(Options = typeof(TaxonomySettingOptions))]
    public string[] TaxonomyContentItemIds { get; set; } = [];
}
