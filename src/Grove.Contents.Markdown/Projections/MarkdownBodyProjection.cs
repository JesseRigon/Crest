using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.AspNetCore.Html;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Models;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Services;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Markdown.ViewModels;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Contents.Markdown.Projections;

/// <summary>An item's rendered body (a read projection): its Markdown as HTML, Liquid rendered when the attachment says so, shortcodes processed, sanitized when asked.</summary>
public sealed class MarkdownBodyProjection(
    IContentDefinitionManager contentDefinitionManager,
    IShortcodeService shortcodeService,
    IMarkdownService markdownService,
    IHtmlSanitizerService htmlSanitizerService,
    ILiquidTemplateManager liquidTemplateManager,
    HtmlEncoder htmlEncoder) : ContentFieldSetProjection<MarkdownBodyFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, MarkdownBodyFieldSet part)
    {
        return context.ForAsync<BodyAspect>(async bodyAspect =>
        {
            try
            {
                var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
                var contentTypeFieldSetDefinition = contentTypeDefinition.FieldSets
                    .FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, "MarkdownBody", StringComparison.Ordinal));
                var settings = contentTypeFieldSetDefinition.GetSettings<MarkdownBodyFieldSetSettings>();

                var markdown = part.Markdown ?? string.Empty;
                if (settings.RenderLiquid)
                {
                    var model = new MarkdownBodyFieldSetViewModel()
                    {
                        Markdown = markdown,
                        MarkdownBodyFieldSet = part,
                        ContentItem = part.ContentItem,
                    };

                    markdown = await liquidTemplateManager.RenderStringAsync(model.Markdown, htmlEncoder, model,
                        new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(model.ContentItem) });
                }

                // The default Markdown option is to entity escape html so filters must be run after the markdown has
                // been processed.
                var html = markdownService.ToHtml(part.Markdown ?? string.Empty);

                html = await shortcodeService.ProcessAsync(html,
                    new Context
                    {
                        ["ContentItem"] = part.ContentItem,
                        ["TypeFieldSetDefinition"] = contentTypeFieldSetDefinition,
                    });

                if (settings.SanitizeHtml)
                {
                    html = htmlSanitizerService.Sanitize(html);
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
