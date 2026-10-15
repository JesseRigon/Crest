using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Grove.Admin.Models;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Localization.ViewModels;

namespace Grove.Localization.Drivers;

public sealed class AdminCulturePickerNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalizationService _localizationService;

    public AdminCulturePickerNavbarDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
    }

    public override async Task<IDisplayResult> DisplayAsync(Navbar model, BuildDisplayContext context)
    {
        var supportedCultures = (await _localizationService.GetSupportedCulturesAsync()).Select(c => CultureInfo.GetCultureInfo(c));

        return Initialize<AdminCulturePickerViewModel>("AdminCulturePicker", model =>
        {
            model.SupportedCultures = supportedCultures;
            model.CurrentCulture = _httpContextAccessor
            .HttpContext
            .Features
            .Get<IRequestCultureFeature>()?.RequestCulture?.Culture ?? CultureInfo.CurrentUICulture;

        }).RenderWhen(static (supportedCultures) => Task.FromResult(supportedCultures.Count() > 1), supportedCultures)
        .Location(PlatformConstants.DisplayType.DetailAdmin, "Content:5");
    }
}
