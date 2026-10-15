using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.Extensions.Localization;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.ContentManagement.Metadata.Models;
using Grove.DisplayManagement.Views;
using Grove.Infrastructure.Html;
using Grove.Liquid;
using Grove.Markdown.Fields;
using Grove.Markdown.Services;
using Grove.Markdown.Settings;
using Grove.Markdown.ViewModels;
using Grove.Mvc.ModelBinding;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Markdown.Drivers;

public sealed class MarkdownFieldDisplayDriver : ContentFieldDisplayDriver<MarkdownField>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IShortcodeService _shortcodeService;
    private readonly IMarkdownService _markdownService;

    internal readonly IStringLocalizer S;

    public MarkdownFieldDisplayDriver(ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder,
        IHtmlSanitizerService htmlSanitizerService,
        IShortcodeService shortcodeService,
        IMarkdownService markdownService,
        IStringLocalizer<MarkdownFieldDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
        _htmlSanitizerService = htmlSanitizerService;
        _shortcodeService = shortcodeService;
        _markdownService = markdownService;
        S = localizer;
    }

    public override IDisplayResult Display(MarkdownField field, BuildFieldDisplayContext context)
    {
        return Initialize<MarkdownFieldViewModel>(GetDisplayShapeType(context), async model =>
        {
            model.Markdown = field.Markdown;
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;

            var settings = context.PartFieldDefinition.GetSettings<MarkdownFieldSettings>();

            if (settings.RenderLiquid)
            {
                model.Markdown = await _liquidTemplateManager.RenderStringAsync(model.Markdown, _htmlEncoder, model,
                    new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(field.ContentItem) });
            }

            // The default Markdown option is to entity escape html
            // so filters must be run after the markdown has been processed.
            model.Html = _markdownService.ToHtml(model.Markdown ?? string.Empty);

            model.Html = await _shortcodeService.ProcessAsync(model.Html,
                new Context
                {
                    ["ContentItem"] = field.ContentItem,
                    ["PartFieldDefinition"] = context.PartFieldDefinition,
                });

            if (settings.SanitizeHtml)
            {
                model.Html = _htmlSanitizerService.Sanitize(model.Html ?? string.Empty);
            }
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content")
        .Location(PlatformConstants.DisplayType.Summary, "Content");
    }
}
