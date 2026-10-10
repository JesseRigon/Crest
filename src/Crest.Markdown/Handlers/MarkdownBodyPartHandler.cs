using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.AspNetCore.Html;
using Crest.ContentManagement.Handlers;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Models;
using Crest.Infrastructure.Html;
using Crest.Liquid;
using Crest.Markdown.Models;
using Crest.Markdown.Services;
using Crest.Markdown.Settings;
using Crest.Markdown.ViewModels;
using Crest.Shortcodes.Services;
using Shortcodes;

namespace Crest.Markdown.Handlers;

public class MarkdownBodyPartHandler : ContentPartHandler<MarkdownBodyPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IShortcodeService _shortcodeService;
    private readonly IMarkdownService _markdownService;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;

    public MarkdownBodyPartHandler(IContentDefinitionManager contentDefinitionManager,
        IShortcodeService shortcodeService,
        IMarkdownService markdownService,
        IHtmlSanitizerService htmlSanitizerService,
        ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _shortcodeService = shortcodeService;
        _markdownService = markdownService;
        _htmlSanitizerService = htmlSanitizerService;
        _liquidTemplateManager = liquidTemplateManager;
        _htmlEncoder = htmlEncoder;
    }

    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, MarkdownBodyPart part)
    {
        return context.ForAsync<BodyAspect>(async bodyAspect =>
        {
            try
            {
                var contentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
                var contentTypePartDefinition = contentTypeDefinition.Parts
                    .FirstOrDefault(x => string.Equals(x.PartDefinition.Name, "MarkdownBodyPart", StringComparison.Ordinal));
                var settings = contentTypePartDefinition.GetSettings<MarkdownBodyPartSettings>();

                var markdown = part.Markdown ?? string.Empty;
                if (settings.RenderLiquid)
                {
                    var model = new MarkdownBodyPartViewModel()
                    {
                        Markdown = markdown,
                        MarkdownBodyPart = part,
                        ContentItem = part.ContentItem,
                    };

                    markdown = await _liquidTemplateManager.RenderStringAsync(model.Markdown, _htmlEncoder, model,
                        new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(model.ContentItem) });
                }

                // The default Markdown option is to entity escape html so filters must be run after the markdown has
                // been processed.
                var html = _markdownService.ToHtml(part.Markdown ?? string.Empty);

                html = await _shortcodeService.ProcessAsync(html,
                    new Context
                    {
                        ["ContentItem"] = part.ContentItem,
                        ["TypePartDefinition"] = contentTypePartDefinition,
                    });

                if (settings.SanitizeHtml)
                {
                    html = _htmlSanitizerService.Sanitize(html);
                }

                bodyAspect.Body = new HtmlString(html);
            }
            catch
            {
                bodyAspect.Body = HtmlString.Empty;
            }
        });
    }
}
