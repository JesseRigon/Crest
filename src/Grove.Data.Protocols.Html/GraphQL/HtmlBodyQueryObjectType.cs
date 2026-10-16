using System.Text.Encodings.Web;
using Fluid.Values;
using GraphQL;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Grove.Data.Protocols.GraphQL;
using Grove.Contents.Management.Metadata;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Settings;
using Grove.Data.Protocols.Html.ViewModels;
using Grove.Display.Liquid;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Data.Protocols.Html.GraphQL;

public class HtmlBodyQueryObjectType : ObjectGraphType<HtmlBodyFieldSet>
{
    public HtmlBodyQueryObjectType(IStringLocalizer<HtmlBodyQueryObjectType> S)
    {
        Name = "HtmlBodyFieldSet";
        Description = S["Content stored as HTML."];

        Field<StringGraphType>("html")
            .Description(S["the HTML content"])
            .ResolveLockedAsync(RenderHtml);
    }

    private static async ValueTask<object> RenderHtml(IResolveFieldContext<HtmlBodyFieldSet> ctx)
    {
        var shortcodeService = ctx.RequestServices.GetRequiredService<IShortcodeService>();
        var contentDefinitionManager = ctx.RequestServices.GetRequiredService<IContentDefinitionManager>();

        var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(ctx.Source.ContentItem.ContentType);
        var contentTypeFieldSetDefinition = contentTypeDefinition.FieldSets.FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, "HtmlBody", StringComparison.Ordinal));
        var settings = contentTypeFieldSetDefinition.GetSettings<HtmlBodyFieldSetSettings>();

        var html = ctx.Source.Html;

        if (settings.RenderLiquid)
        {
            var model = new HtmlBodyFieldSetViewModel()
            {
                Html = ctx.Source.Html,
                HtmlBodyFieldSet = ctx.Source,
                ContentItem = ctx.Source.ContentItem,
            };
            var liquidTemplateManager = ctx.RequestServices.GetRequiredService<ILiquidTemplateManager>();
            var htmlEncoder = ctx.RequestServices.GetService<HtmlEncoder>();

            html = await liquidTemplateManager.RenderStringAsync(html, htmlEncoder, model, new Dictionary<string, FluidValue> { ["ContentItem"] = new ObjectValue(model.ContentItem) });
        }

        return await shortcodeService.ProcessAsync(html,
            new Context
            {
                ["ContentItem"] = ctx.Source.ContentItem,
                ["TypeFieldSetDefinition"] = contentTypeFieldSetDefinition,
            });
    }
}
