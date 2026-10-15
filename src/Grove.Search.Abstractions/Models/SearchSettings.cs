using System.ComponentModel.DataAnnotations;
using Grove.Data.Indexing;
using Grove.Infrastructure.Settings;

namespace Grove.Search.Models;

public class SearchSettings
{
    [Display(Name = "Default search index", Description = "The default index to use for the search page.", Order = 0)]
    [Setting(Options = typeof(IndexProfileOptions), Placeholder = "Select a default index")]
    public string DefaultIndexProfileName { get; set; }

    [Obsolete("This property is no longer used. Instead use DefaultIndexId property to set the IndexProfile.Id.")]
    [ScaffoldColumn(false)]
    public string ProviderName { get; set; }

    [Display(Name = "Placeholder", Description = "The placeholder text to use in the search bar.", Order = 1)]
    public string Placeholder { get; set; }

    [Display(Name = "Page title", Description = "The title of the search page.", Order = 2)]
    public string PageTitle { get; set; }
}

/// <summary>The tenant's indexes, by name.</summary>
public sealed class IndexProfileOptions(IIndexProfileStore indexes) : ISettingOptions
{
    public async Task<IReadOnlyList<SettingOption>> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        [.. (await indexes.GetAllAsync()).Select(index => new SettingOption(index.Name, index.Name))];
}
