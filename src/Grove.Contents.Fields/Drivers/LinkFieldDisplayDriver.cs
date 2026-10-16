using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Extensions;
using Grove.Display.Management.Views;
using Grove.Infrastructure.Html;

namespace Grove.Contents.Fields.Drivers;

public sealed class LinkFieldDisplayDriver : ContentFieldDisplayDriver<LinkField>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly HtmlEncoder _htmlencoder;

    internal readonly IStringLocalizer S;

    public LinkFieldDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<LinkFieldDisplayDriver> localizer,
        IHtmlSanitizerService htmlSanitizerService,
        HtmlEncoder htmlencoder)
    {
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
            model.FieldSet = context.ContentFieldSet;
            model.FieldDefinition = context.FieldDefinition;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
