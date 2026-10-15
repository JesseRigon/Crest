using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Grove.Admin.Models;
using Grove.ContentLocalization.ViewModels;
using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Localization;

namespace Grove.ContentLocalization.Drivers;

public sealed class ContentCulturePickerNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalizationService _localizationService;

    public ContentCulturePickerNavbarDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _localizationService = localizationService;
    }

    public override async Task<IDisplayResult> DisplayAsync(Navbar model, BuildDisplayContext context)
    {
        var supportedCultures = (await _localizationService.GetSupportedCulturesAsync()).Select(c => CultureInfo.GetCultureInfo(c));

        return Initialize<CulturePickerViewModel>("ContentCulturePicker", model =>
        {
            model.SupportedCultures = supportedCultures;
            model.CurrentCulture = _httpContextAccessor
            .HttpContext
            .Features
            .Get<IRequestCultureFeature>()?.RequestCulture?.Culture ?? CultureInfo.CurrentUICulture;
        }).RenderWhen(static (supportedCultures) => Task.FromResult(supportedCultures.Count() > 1), supportedCultures)
        .Location(PlatformConstants.DisplayType.Detail, "Content:5");
    }
}
