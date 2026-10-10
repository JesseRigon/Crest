using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Fluid.Values;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Crest.Apis.GraphQL;
using Crest.ContentFields.Fields;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.Liquid;
using Crest.Shortcodes.Services;
using Shortcodes;

namespace Crest.ContentFields.GraphQL;

public class HtmlFieldQueryObjectType : ObjectGraphType<HtmlField>
{
    public HtmlFieldQueryObjectType(IStringLocalizer<HtmlFieldQueryObjectType> S)
    {
        Name = nameof(HtmlField);
        Description = S["Content stored as HTML."];

        Field<StringGraphType>("html")
            .Description(S["the HTML content"])
            .ResolveLockedAsync(RenderHtml);
    }

    private static async ValueTask<object> RenderHtml(IResolveFieldContext<HtmlField> ctx)
    {
        var serviceProvider = ctx.RequestServices;
        var shortcodeService = serviceProvider.GetRequiredService<IShortcodeService>();
        var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();

        var jObject = (JsonObject)ctx.Source.Content;
        // The JObject.Path is consistent here even when contained in a bag part.
        var jsonPath = jObject.GetNormalizedPath();
        var paths = jsonPath.Split('.');
        var partName = paths[0];
        var fieldName = paths[1];
        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(ctx.Source.ContentItem.ContentType);
        var contentPartDefinition = contentTypeDefinition.Parts.FirstOrDefault(x => string.Equals(x.Name, partName, StringComparison.Ordinal));
        var contentPartFieldDefinition = contentPartDefinition.PartDefinition.Fields.FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.Ordinal));
        var settings = contentPartFieldDefinition.GetSettings<HtmlFieldSettings>();

        var html = ctx.Source.Html;

        if (settings.RenderLiquid)
        {
            var model = new EditHtmlFieldViewModel()
            {
                Html = ctx.Source.Html,
                Field = ctx.Source,
                Part = ctx.Source.ContentItem.Get<ContentPart>(partName),
                PartFieldDefinition = contentPartFieldDefinition,
            };
            var liquidTemplateManager = serviceProvider.GetRequiredService<ILiquidTemplateManager>();
            var htmlEncoder = serviceProvider.GetService<HtmlEncoder>();

            html = await liquidTemplateManager.RenderStringAsync(html, htmlEncoder, model,
                new Dictionary<string, FluidValue>() { ["ContentItem"] = new ObjectValue(ctx.Source.ContentItem) });
        }

        return await shortcodeService.ProcessAsync(html,
            new Context
            {
                ["ContentItem"] = ctx.Source.ContentItem,
                ["PartFieldDefinition"] = contentPartFieldDefinition,
            });
    }
}
