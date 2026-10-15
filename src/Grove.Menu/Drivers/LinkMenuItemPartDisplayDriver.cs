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

namespace Grove.Menu.Drivers;

public sealed class LinkMenuItemPartDisplayDriver : ContentPartDisplayDriver<LinkMenuItemPart>
{
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly HtmlEncoder _htmlEncoder;

    internal readonly IStringLocalizer S;

    public LinkMenuItemPartDisplayDriver(
        IUrlHelperFactory urlHelperFactory,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LinkMenuItemPartDisplayDriver> localizer,
        IHtmlSanitizerService htmlSanitizerService,
        HtmlEncoder htmlEncoder
        )
    {
        _urlHelperFactory = urlHelperFactory;
        _httpContextAccessor = httpContextAccessor;
        _htmlSanitizerService = htmlSanitizerService;
        _htmlEncoder = htmlEncoder;
        S = localizer;
    }

    public override IDisplayResult Display(LinkMenuItemPart part, BuildPartDisplayContext context)
    {
        return Combine(
            Dynamic("LinkMenuItemPart_Admin", static (shape, part) =>
            {
                shape.MenuItemPart = part;
            }, part)
            .Location("Admin", "Content:10"),
            Dynamic("LinkMenuItemPart_Thumbnail", static (shape, part) =>
            {
                shape.MenuItemPart = part;
            }, part)
            .Location("Thumbnail", "Content:10")
        );
    }
}
