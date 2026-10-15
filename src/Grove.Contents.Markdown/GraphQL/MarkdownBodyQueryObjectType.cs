using System.Text.Encodings.Web;
using Fluid.Values;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management.Metadata;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Models;
using Grove.Contents.Markdown.Services;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Markdown.ViewModels;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Contents.Markdown.GraphQL;

public class MarkdownBodyQueryObjectType : ObjectGraphType<MarkdownBodyPart>
{
    public MarkdownBodyQueryObjectType(IStringLocalizer<MarkdownBodyQueryObjectType> S)
    {
        Name = nameof(MarkdownBodyPart);
        Description = S["Content stored as Markdown. You can also query the HTML interpreted version of Markdown."];

        Field("markdown", x => x.Markdown, nullable: true)
            .Description(S["the markdown value"]);
        Field<StringGraphType>("html")
            .Description(S["the HTML representation of the markdown content"])
            .ResolveLockedAsync(ToHtml);
    }

    private static async ValueTask<object> ToHtml(IResolveFieldContext<MarkdownBodyPart> ctx)
    {
        if (string.IsNullOrEmpty(ctx.Source.Markdown))
        {
            return ctx.Source.Markdown;
        }

        var serviceProvider = ctx.RequestServices;
        var markdownService = serviceProvider.GetRequiredService<IMarkdownService>();
        var shortcodeService = serviceProvider.GetRequiredService<IShortcodeService>();
        var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();

        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(ctx.Source.ContentItem.ContentType);
        var contentTypePartDefinition = contentTypeDefinition.Parts
            .FirstOrDefault(x => string.Equals(x.PartDefinition.Name, "MarkdownBodyPart", StringComparison.Ordinal));
        var settings = contentTypePartDefinition.GetSettings<MarkdownBodyPartSettings>();

        var markdown = ctx.Source.Markdown ?? string.Empty;
        if (settings.RenderLiquid)
        {
            var liquidTemplateManager = serviceProvider.GetService<ILiquidTemplateManager>();
            var htmlEncoder = serviceProvider.GetService<HtmlEncoder>();
            var model = new MarkdownBodyPartViewModel()
            {
                Markdown = markdown,
                MarkdownBodyPart = ctx.Source,
                ContentItem = ctx.Source.ContentItem,
            };

            markdown = await liquidTemplateManager.RenderStringAsync(model.Markdown, htmlEncoder, model,
                new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(model.ContentItem) });
        }

        // The default Markdown option is to entity escape html so filters must be run after the markdown has been
        // processed.
        var html = markdownService.ToHtml(markdown ?? string.Empty);

        html = await shortcodeService.ProcessAsync(html,
            new Context
            {
                ["ContentItem"] = ctx.Source.ContentItem,
                ["PartFieldDefinition"] = contentTypePartDefinition,
            });

        if (settings.SanitizeHtml)
        {
            var htmlSanitizerService = serviceProvider.GetRequiredService<IHtmlSanitizerService>();
            html = htmlSanitizerService.Sanitize(html);
        }

        return html;
    }
}
