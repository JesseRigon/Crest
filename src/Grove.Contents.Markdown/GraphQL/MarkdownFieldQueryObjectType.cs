using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Fluid.Values;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Infrastructure.Html;
using Grove.Display.Liquid;
using Grove.Contents.Markdown.Fields;
using Grove.Contents.Markdown.Services;
using Grove.Contents.Markdown.Settings;
using Grove.Contents.Markdown.ViewModels;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Contents.Markdown.GraphQL;

public class MarkdownFieldQueryObjectType : ObjectGraphType<MarkdownField>
{
    public MarkdownFieldQueryObjectType(IStringLocalizer<MarkdownFieldQueryObjectType> S)
    {
        Name = nameof(MarkdownField);
        Description = S["Content stored as Markdown. You can also query the HTML interpreted version of Markdown."];

        Field("markdown", x => x.Markdown, nullable: true)
            .Description(S["the markdown value"]);
        Field<StringGraphType>("html")
            .Description(S["the HTML representation of the markdown content"])
            .ResolveLockedAsync(ToHtml);
    }

    private static async ValueTask<object> ToHtml(IResolveFieldContext<MarkdownField> ctx)
    {
        if (string.IsNullOrEmpty(ctx.Source.Markdown))
        {
            return ctx.Source.Markdown;
        }

        var serviceProvider = ctx.RequestServices;
        var markdownService = serviceProvider.GetRequiredService<IMarkdownService>();
        var shortcodeService = serviceProvider.GetRequiredService<IShortcodeService>();

        var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();

        var jObject = (JsonObject)ctx.Source.Content;
        // The JObject.Path is consistent here even when contained in a bag part.
        var jsonPath = jObject.GetNormalizedPath();
        var paths = jsonPath.Split('.');
        var partName = paths[0];
        var fieldName = paths[1];
        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(ctx.Source.ContentItem.ContentType);
        var contentFieldSetDefinition = contentTypeDefinition.FieldSets
            .FirstOrDefault(x => string.Equals(x.Name, partName, StringComparison.Ordinal));
        var contentFieldDefinition = contentFieldSetDefinition.FieldSetDefinition.Fields
            .FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.Ordinal));

        var settings = contentFieldDefinition.GetSettings<MarkdownFieldSettings>();

        var markdown = ctx.Source.Markdown ?? string.Empty;
        if (settings.RenderLiquid)
        {
            var liquidTemplateManager = serviceProvider.GetService<ILiquidTemplateManager>();
            var htmlEncoder = serviceProvider.GetService<HtmlEncoder>();

            var model = new MarkdownFieldViewModel()
            {
                Markdown = markdown,
                Field = ctx.Source,
                FieldSet = ctx.Source.ContentItem.Get<ContentFieldSet>(partName),
                FieldDefinition = contentFieldDefinition,
            };

            markdown = await liquidTemplateManager.RenderStringAsync(model.Markdown, htmlEncoder, model,
                new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(ctx.Source.ContentItem) });
        }

        // The default Markdown option is to entity escape html so filters must be run after the markdown has been
        // processed.
        var html = markdownService.ToHtml(markdown ?? string.Empty);

        html = await shortcodeService.ProcessAsync(html,
            new Context
            {
                ["ContentItem"] = ctx.Source.ContentItem,
                ["FieldDefinition"] = contentFieldDefinition,
            });

        if (settings.SanitizeHtml)
        {
            var htmlSanitizerService = serviceProvider.GetRequiredService<IHtmlSanitizerService>();
            html = htmlSanitizerService.Sanitize(html);
        }

        return html;
    }
}
