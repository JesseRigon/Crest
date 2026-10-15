using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Views;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Services;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Markdown.ViewModels;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Contents.Markdown.Drivers;

public sealed class MarkdownBodyPartDisplayDriver : ContentPartDisplayDriver<MarkdownBodyPart>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IShortcodeService _shortcodeService;
    private readonly IMarkdownService _markdownService;

    internal readonly IStringLocalizer S;

    public MarkdownBodyPartDisplayDriver(ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder,
        IHtmlSanitizerService htmlSanitizerService,
        IShortcodeService shortcodeService,
        IMarkdownService markdownService,
        IStringLocalizer<MarkdownBodyPartDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
        _htmlSanitizerService = htmlSanitizerService;
        _shortcodeService = shortcodeService;
        _markdownService = markdownService;
        S = localizer;
    }

    public override IDisplayResult Display(MarkdownBodyPart markdownBodyPart, BuildPartDisplayContext context)
    {
        return Initialize<MarkdownBodyPartViewModel, MarkdownBodyPartDisplayDriver, MarkdownBodyPart, BuildPartDisplayContext>(GetDisplayShapeType(context), static (m, driver, part, context) => driver.BuildViewModel(m, part, context), this, markdownBodyPart, context)
            .Location(PlatformConstants.DisplayType.Detail, "Content")
            .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    private async ValueTask BuildViewModel(MarkdownBodyPartViewModel model, MarkdownBodyPart markdownBodyPart, BuildPartDisplayContext context)
    {
        model.Markdown = markdownBodyPart.Markdown;
        model.MarkdownBodyPart = markdownBodyPart;
        model.ContentItem = markdownBodyPart.ContentItem;

        var settings = context.TypePartDefinition.GetSettings<MarkdownBodyPartSettings>();

        if (settings.RenderLiquid)
        {
            model.Markdown = await _liquidTemplateManager.RenderStringAsync(model.Markdown, _htmlEncoder, model,
                new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(model.ContentItem) });
        }

        // The default Markdown option is to entity escape html so filters must be run after the markdown has been
        // processed.
        model.Html = _markdownService.ToHtml(model.Markdown ?? "");

        model.Html = await _shortcodeService.ProcessAsync(model.Html,
            new Context
            {
                ["ContentItem"] = markdownBodyPart.ContentItem,
                ["TypePartDefinition"] = context.TypePartDefinition,
            });

        if (settings.SanitizeHtml)
        {
            model.Html = _htmlSanitizerService.Sanitize(model.Html ?? "");
        }
    }
}
