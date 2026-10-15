using Microsoft.Extensions.Localization;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.Menu.Services;

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
