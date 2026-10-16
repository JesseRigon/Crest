using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Services;
using Grove.Data.Protocols.Html.Settings;
using Grove.Data.Protocols.Html.ViewModels;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;
using Shortcodes;

namespace Grove.Data.Protocols.Html.Drivers;

public sealed class HtmlBodyFieldSetDisplayDriver : ContentFieldSetDisplayDriver<HtmlBodyFieldSet>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IHtmlDisplayService _htmlDisplayService;

    internal readonly IStringLocalizer S;

    public HtmlBodyFieldSetDisplayDriver(ILiquidTemplateManager liquidTemplateManager,
        IHtmlSanitizerService htmlSanitizerService,
        IHtmlDisplayService htmlDisplayService,
        IStringLocalizer<HtmlBodyFieldSetDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlSanitizerService = htmlSanitizerService;
        _htmlDisplayService = htmlDisplayService;
        S = localizer;
    }

    public override IDisplayResult Display(HtmlBodyFieldSet htmlBodyFieldSet, BuildFieldSetDisplayContext context)
    {
        return Initialize<HtmlBodyFieldSetViewModel, HtmlBodyFieldSetDisplayDriver, HtmlBodyFieldSet, BuildFieldSetDisplayContext>(GetDisplayShapeType(context), static (m, driver, part, context) => driver.BuildViewModelAsync(m, part, context), this, htmlBodyFieldSet, context)
            .Location(PlatformConstants.DisplayType.Detail, "Content")
            .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    private async ValueTask BuildViewModelAsync(HtmlBodyFieldSetViewModel model, HtmlBodyFieldSet htmlBodyFieldSet, BuildFieldSetDisplayContext context)
    {
        model.Html = htmlBodyFieldSet.Html;
        model.HtmlBodyFieldSet = htmlBodyFieldSet;
        model.ContentItem = htmlBodyFieldSet.ContentItem;

        var settings = context.TypeFieldSetDefinition.GetSettings<HtmlBodyFieldSetSettings>();

        await _htmlDisplayService.UpdateModelHtmlAsync(
            model,
            settings.RenderLiquid,
            new Context { ["TypeFieldSetDefinition"] = context.TypeFieldSetDefinition },
            settings.SanitizeHtml);
    }
}
