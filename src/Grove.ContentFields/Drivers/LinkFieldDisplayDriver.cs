using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Extensions;
using Grove.DisplayManagement.Views;
using Grove.Infrastructure.Html;
using Grove.Mvc.ModelBinding;

namespace Grove.ContentFields.Drivers;

public sealed class LinkFieldDisplayDriver : ContentFieldDisplayDriver<LinkField>
{
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly HtmlEncoder _htmlencoder;

    internal readonly IStringLocalizer S;

    public LinkFieldDisplayDriver(
        IUrlHelperFactory urlHelperFactory,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LinkFieldDisplayDriver> localizer,
        IHtmlSanitizerService htmlSanitizerService,
        HtmlEncoder htmlencoder)
    {
        _urlHelperFactory = urlHelperFactory;
        _httpContextAccessor = httpContextAccessor;
        S = localizer;
        _htmlSanitizerService = htmlSanitizerService;
        _htmlencoder = htmlencoder;
    }

    public override IDisplayResult Display(LinkField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayLinkFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
