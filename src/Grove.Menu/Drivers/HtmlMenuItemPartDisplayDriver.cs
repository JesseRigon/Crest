using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Extensions;
using Grove.DisplayManagement.Views;
using Grove.Infrastructure.Html;
using Grove.Menu.Models;
using Grove.Menu.Settings;

namespace Grove.Menu.Drivers;

public sealed class HtmlMenuItemPartDisplayDriver : ContentPartDisplayDriver<HtmlMenuItemPart>
{
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly HtmlEncoder _htmlencoder;

    internal readonly IStringLocalizer S;

    public HtmlMenuItemPartDisplayDriver(
        IUrlHelperFactory urlHelperFactory,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<HtmlMenuItemPartDisplayDriver> localizer,
        IHtmlSanitizerService htmlSanitizerService,
        HtmlEncoder htmlencoder
        )
    {
        _urlHelperFactory = urlHelperFactory;
        _httpContextAccessor = httpContextAccessor;
        _htmlSanitizerService = htmlSanitizerService;
        _htmlencoder = htmlencoder;
        S = localizer;
    }

    public override IDisplayResult Display(HtmlMenuItemPart part, BuildPartDisplayContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<HtmlMenuItemPartSettings>();

        if (settings.SanitizeHtml)
        {
            part.Html = _htmlSanitizerService.Sanitize(part.Html);
        }

        return Combine(
            Dynamic("HtmlMenuItemPart_Admin", static (shape, part) =>
            {
                shape.MenuItemPart = part;
            }, part)
            .Location("Admin", "Content:10"),
            Dynamic("HtmlMenuItemPart_Thumbnail", static (shape, part) =>
            {
                shape.MenuItemPart = part;
            }, part)
            .Location("Thumbnail", "Content:10")
        );
    }
}
