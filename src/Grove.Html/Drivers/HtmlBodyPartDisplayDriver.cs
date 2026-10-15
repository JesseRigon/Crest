using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Html.Models;
using Grove.Html.Services;
using Grove.Html.Settings;
using Grove.Html.ViewModels;
using Grove.Infrastructure.Html;
using Grove.Liquid;
using Grove.Mvc.ModelBinding;
using Shortcodes;

namespace Grove.Html.Drivers;

public sealed class HtmlBodyPartDisplayDriver : ContentPartDisplayDriver<HtmlBodyPart>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IHtmlDisplayService _htmlDisplayService;

    internal readonly IStringLocalizer S;

    public HtmlBodyPartDisplayDriver(ILiquidTemplateManager liquidTemplateManager,
        IHtmlSanitizerService htmlSanitizerService,
        IHtmlDisplayService htmlDisplayService,
        IStringLocalizer<HtmlBodyPartDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlSanitizerService = htmlSanitizerService;
        _htmlDisplayService = htmlDisplayService;
        S = localizer;
    }

    public override IDisplayResult Display(HtmlBodyPart htmlBodyPart, BuildPartDisplayContext context)
    {
        return Initialize<HtmlBodyPartViewModel, HtmlBodyPartDisplayDriver, HtmlBodyPart, BuildPartDisplayContext>(GetDisplayShapeType(context), static (m, driver, part, context) => driver.BuildViewModelAsync(m, part, context), this, htmlBodyPart, context)
            .Location(PlatformConstants.DisplayType.Detail, "Content")
            .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    private async ValueTask BuildViewModelAsync(HtmlBodyPartViewModel model, HtmlBodyPart htmlBodyPart, BuildPartDisplayContext context)
    {
        model.Html = htmlBodyPart.Html;
        model.HtmlBodyPart = htmlBodyPart;
        model.ContentItem = htmlBodyPart.ContentItem;

        var settings = context.TypePartDefinition.GetSettings<HtmlBodyPartSettings>();

        await _htmlDisplayService.UpdateModelHtmlAsync(
            model,
            settings.RenderLiquid,
            new Context { ["TypePartDefinition"] = context.TypePartDefinition },
            settings.SanitizeHtml);
    }
}
