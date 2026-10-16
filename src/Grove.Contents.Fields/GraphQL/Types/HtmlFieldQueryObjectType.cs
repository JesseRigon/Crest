using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Fluid.Values;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Fields.Fields;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Fields.ViewModels;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Display.Liquid;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Contents.Fields.GraphQL;

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
        var contentFieldSetDefinition = contentTypeDefinition.FieldSets.FirstOrDefault(x => string.Equals(x.Name, partName, StringComparison.Ordinal));
        var contentFieldDefinition = contentFieldSetDefinition.FieldSetDefinition.Fields.FirstOrDefault(x => string.Equals(x.Name, fieldName, StringComparison.Ordinal));
        var settings = contentFieldDefinition.GetSettings<HtmlFieldSettings>();

        var html = ctx.Source.Html;

        if (settings.RenderLiquid)
        {
            var model = new EditHtmlFieldViewModel()
            {
                Html = ctx.Source.Html,
                Field = ctx.Source,
                FieldSet = ctx.Source.ContentItem.Get<ContentFieldSet>(partName),
                FieldDefinition = contentFieldDefinition,
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
                ["FieldDefinition"] = contentFieldDefinition,
            });
    }
}
