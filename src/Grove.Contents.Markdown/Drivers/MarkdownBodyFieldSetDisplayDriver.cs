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

public sealed class MarkdownBodyFieldSetDisplayDriver : ContentFieldSetDisplayDriver<MarkdownBodyFieldSet>
{
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly IShortcodeService _shortcodeService;
    private readonly IMarkdownService _markdownService;

    internal readonly IStringLocalizer S;

    public MarkdownBodyFieldSetDisplayDriver(ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder,
        IHtmlSanitizerService htmlSanitizerService,
        IShortcodeService shortcodeService,
        IMarkdownService markdownService,
        IStringLocalizer<MarkdownBodyFieldSetDisplayDriver> localizer)
    {
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
        _htmlSanitizerService = htmlSanitizerService;
        _shortcodeService = shortcodeService;
        _markdownService = markdownService;
        S = localizer;
    }

    public override IDisplayResult Display(MarkdownBodyFieldSet markdownBodyFieldSet, BuildFieldSetDisplayContext context)
    {
        return Initialize<MarkdownBodyFieldSetViewModel, MarkdownBodyFieldSetDisplayDriver, MarkdownBodyFieldSet, BuildFieldSetDisplayContext>(GetDisplayShapeType(context), static (m, driver, part, context) => driver.BuildViewModel(m, part, context), this, markdownBodyFieldSet, context)
            .Location(PlatformConstants.DisplayType.Detail, "Content")
            .Location(PlatformConstants.DisplayType.Summary, "Content");
    }

    private async ValueTask BuildViewModel(MarkdownBodyFieldSetViewModel model, MarkdownBodyFieldSet markdownBodyFieldSet, BuildFieldSetDisplayContext context)
    {
        model.Markdown = markdownBodyFieldSet.Markdown;
        model.MarkdownBodyFieldSet = markdownBodyFieldSet;
        model.ContentItem = markdownBodyFieldSet.ContentItem;

        var settings = context.TypeFieldSetDefinition.GetSettings<MarkdownBodyFieldSetSettings>();

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
                ["ContentItem"] = markdownBodyFieldSet.ContentItem,
                ["TypeFieldSetDefinition"] = context.TypeFieldSetDefinition,
            });

        if (settings.SanitizeHtml)
        {
            model.Html = _htmlSanitizerService.Sanitize(model.Html ?? "");
        }
    }
}
