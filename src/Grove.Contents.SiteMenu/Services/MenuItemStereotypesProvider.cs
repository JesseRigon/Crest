using Microsoft.Extensions.Localization;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.SiteMenu.Services;

public class MenuItemStereotypesProvider : IStereotypesProvider
{
    protected readonly IStringLocalizer S;

    public MenuItemStereotypesProvider(IStringLocalizer<MenuItemStereotypesProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public Task<IEnumerable<StereotypeDescription>> GetStereotypesAsync()
        => Task.FromResult<IEnumerable<StereotypeDescription>>(
          [new StereotypeDescription { Stereotype = "MenuItem", DisplayName = S["Menu Item"] }]);
}
