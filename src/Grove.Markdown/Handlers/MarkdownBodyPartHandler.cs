using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.AspNetCore.Html;
using Grove.ContentManagement.Handlers;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Models;
using Grove.Infrastructure.Html;
using Grove.Liquid;
using Grove.Markdown.Models;
using Grove.Markdown.Services;
using Grove.Markdown.Settings;
using Grove.Markdown.ViewModels;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Markdown.Handlers;

public class MarkdownBodyPartHandler : ContentPartHandler<MarkdownBodyPart>
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IShortcodeService _shortcodeService;
    private readonly IMarkdownService _markdownService;
    private readonly IHtmlSanitizerService _htmlSanitizerService;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly Microsoft.Extensions.Localization.IStringLocalizer S;

    public MarkdownBodyPartHandler(IContentDefinitionManager contentDefinitionManager,
        IShortcodeService shortcodeService,
        IMarkdownService markdownService,
        IHtmlSanitizerService htmlSanitizerService,
        ILiquidTemplateManager liquidTemplateManager,
        HtmlEncoder htmlEncoder,
        Microsoft.Extensions.Localization.IStringLocalizer<MarkdownBodyPartHandler> stringLocalizer)
    {
        S = stringLocalizer;
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

    // Every save path: a body that renders Liquid must parse.
    protected override Task ValidatingAsync(ValidateContentPartContext context, MarkdownBodyPart part)
    {
        var settings = context.ContentTypePartDefinition?.GetSettings<MarkdownBodyPartSettings>();
        if (settings is { RenderLiquid: true } && !string.IsNullOrEmpty(part.Markdown) && !_liquidTemplateManager.Validate(part.Markdown, out var errors))
        {
            context.Fail(S["The Markdown doesn't contain a valid Liquid expression. Details: {0}", string.Join(" ", errors)], nameof(part.Markdown));
        }

        return Task.CompletedTask;
    }
}
