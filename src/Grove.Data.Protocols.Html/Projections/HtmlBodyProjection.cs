using System.Text.Encodings.Web;
using Fluid.Values;
using Microsoft.AspNetCore.Html;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Models;
using Grove.Data.Protocols.Html.Models;
using Grove.Data.Protocols.Html.Settings;
using Grove.Data.Protocols.Html.ViewModels;
using Grove.Display.Liquid;
using Grove.Shortcodes.Services;
using Shortcodes;

namespace Grove.Data.Protocols.Html.Projections;

/// <summary>An item's rendered body (a read projection): its HTML, Liquid rendered when the attachment says so, shortcodes processed.</summary>
public sealed class HtmlBodyProjection(
    IContentDefinitionManager contentDefinitionManager,
    IShortcodeService shortcodeService,
    ILiquidTemplateManager liquidTemplateManager,
    HtmlEncoder htmlEncoder) : ContentFieldSetProjection<HtmlBodyFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, HtmlBodyFieldSet part)
    {
        return context.ForAsync<BodyAspect>(async bodyAspect =>
        {
            try
            {
                var contentTypeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(part.ContentItem.ContentType);
                var contentTypeFieldSetDefinition = contentTypeDefinition.FieldSets.FirstOrDefault(x => string.Equals(x.FieldSetDefinition.Name, "HtmlBody", StringComparison.Ordinal));
                var settings = contentTypeFieldSetDefinition?.GetSettings<HtmlBodyFieldSetSettings>() ?? new();

                var html = part.Html;

                if (settings.RenderLiquid)
                {
                    var model = new HtmlBodyFieldSetViewModel()
                    {
                        Html = part.Html,
                        HtmlBodyFieldSet = part,
                        ContentItem = part.ContentItem,
                    };

                    html = await liquidTemplateManager.RenderStringAsync(html, htmlEncoder, model,
                        new Dictionary<string, FluidValue> { ["ContentItem"] = new ObjectValue(model.ContentItem) });
                }

                html = await shortcodeService.ProcessAsync(html,
                    new Context
                    {
                        ["ContentItem"] = part.ContentItem,
                        ["TypeFieldSetDefinition"] = contentTypeFieldSetDefinition,
                    });

                bodyAspect.Body = new HtmlString(html);
            }
            catch
            {
                bodyAspect.Body = HtmlString.Empty;
            }
        });
    }
}
