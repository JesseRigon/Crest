using Microsoft.Extensions.Localization;
using Grove.ContentFields.Fields;
using Grove.ContentFields.Settings;
using Grove.ContentFields.ViewModels;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Html.Services;
using Grove.Infrastructure.Html;
using Grove.Liquid;
using Grove.Mvc.ModelBinding;
using Shortcodes;

namespace Grove.ContentFields.Drivers;

public sealed class HtmlFieldDisplayDriver : ContentFieldDisplayDriver<HtmlField>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IHtmlDisplayService _htmlDisplayService;
    private readonly IHtmlSanitizerService _htmlSanitizerService;

    internal readonly IStringLocalizer S;

    public HtmlFieldDisplayDriver(
        ILiquidTemplateManager liquidTemplateManager,
        IHtmlDisplayService htmlDisplayService,
        IHtmlSanitizerService htmlSanitizerService,
        IStringLocalizer<HtmlFieldDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlDisplayService = htmlDisplayService;
        _htmlSanitizerService = htmlSanitizerService;
        S = localizer;
    }

    public override IDisplayResult Display(HtmlField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayHtmlFieldViewModel, HtmlFieldDisplayDriver, HtmlField, BuildFieldDisplayContext> (GetDisplayShapeType(context), static async (model, driver, field, context) =>
        {
            model.Html = field.Html;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            var settings = context.PartFieldDefinition.GetSettings<HtmlFieldSettings>();

            await driver._htmlDisplayService.UpdateModelHtmlAsync(
                model,
                settings.RenderLiquid,
                new Context { ["PartFieldDefinition"] = context.PartFieldDefinition },
                settings.SanitizeHtml);
        }, this, field, context)
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
