using Microsoft.Extensions.Localization;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Users.Services;

public class CustomUserSettingsStereotypesProvider : IStereotypesProvider
{
    protected readonly IStringLocalizer S;

    public CustomUserSettingsStereotypesProvider(IStringLocalizer<CustomUserSettingsStereotypesProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync()
        => Task.FromResult<IEnumerable<StereotypeDescription>>(
            [new StereotypeDescription { Stereotype = "CustomUserSettings", DisplayName = S["Custom User Settings"] }]);

}
