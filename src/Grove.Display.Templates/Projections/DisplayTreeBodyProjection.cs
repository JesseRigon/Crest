using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Models;
using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;

namespace Grove.Display.Templates.Projections;

/// <summary>An item's body aspect (a read projection; the full-text aspect and feeds read it): its page body's text.</summary>
public sealed class DisplayTreeBodyProjection(ITemplateCatalog templates) : ContentFieldSetProjection<DisplayTreeFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, DisplayTreeFieldSet part) =>
        context.ForAsync<BodyAspect>(async aspect =>
        {
            if (part.Tree is null)
            {
                return;
            }

            var composites = (await templates.ListPublishedAsync())
                .Where(template => template.IsComposite)
                .GroupBy(template => template.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Source, StringComparer.OrdinalIgnoreCase);
            aspect.Body = new Microsoft.AspNetCore.Html.HtmlString(System.Net.WebUtility.HtmlEncode(DisplayTreeText.Extract(part.Tree, composites)));
        });
}
