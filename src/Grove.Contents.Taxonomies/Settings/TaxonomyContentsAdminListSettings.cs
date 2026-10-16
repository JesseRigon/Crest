using System.ComponentModel.DataAnnotations;
using Grove.Infrastructure.Settings;

namespace Grove.Contents.Taxonomies.Settings;

public class TaxonomyContentsAdminListSettings
{
    public const string GroupId = "taxonomyContentsAdminList";

    [Display(Name = "Categories", Description = "The categories to filter the contents list by.")]
    [Setting(Options = typeof(TaxonomySettingOptions))]
    public string[] TaxonomyContentItemIds { get; set; } = [];
}
