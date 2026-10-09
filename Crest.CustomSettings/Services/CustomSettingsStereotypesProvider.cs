using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.CustomSettings.Services;

public class CustomSettingsStereotypesProvider : IStereotypesProvider
{
    protected readonly IStringLocalizer S;

    public CustomSettingsStereotypesProvider(IStringLocalizer<CustomSettingsStereotypesProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync()
        => Task.FromResult<IEnumerable<StereotypeDescription>>(
            [new StereotypeDescription { Stereotype = "CustomSettings", DisplayName = S["Custom Settings"] }]);

}
