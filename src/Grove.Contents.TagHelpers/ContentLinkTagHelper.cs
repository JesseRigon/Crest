using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;

namespace Grove.Contents.TagHelpers;

[HtmlTargetElement("a", Attributes = ContentLinkDisplay)]
/// <summary>
/// <c>display-for</c>: a link to an item's public display. The admin pages are native; their
/// addresses are <see cref="ContentAdminPaths"/>.
/// </summary>
public class ContentLinkTagHelper : TagHelper
{
    private const string ContentLinkDisplay = "display-for";
    private const string RoutePrefix = "asp-route-";

    private readonly IContentManager _contentManager;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ContentLinkTagHelper(
        IContentManager contentManager,
        IUrlHelperFactory urlHelperFactory,
        IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _urlHelperFactory = urlHelperFactory;
        _contentManager = contentManager;
    }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; }

    /// <summary>
    /// Links to the display page of this content item.
    /// </summary>
    [HtmlAttributeName(ContentLinkDisplay)]
    public ContentItem DisplayFor { get; set; }

    public override async Task ProcessAsync(TagHelperContext tagHelperContext, TagHelperOutput output)
    {
        ContentItemMetadata metadata = null;
        ContentItem contentItem = null;

        var urlHelper = _urlHelperFactory.GetUrlHelper(ViewContext);

        if (DisplayFor != null)
        {
            contentItem = DisplayFor;
            var previewAspect = await _contentManager.PopulateAspectAsync<PreviewAspect>(contentItem);

            if (!string.IsNullOrEmpty(previewAspect.PreviewUrl))
            {
                var previewUrl = previewAspect.PreviewUrl;
                if (!previewUrl.StartsWith("~/", StringComparison.OrdinalIgnoreCase))
                {
                    if (previewUrl.StartsWith('/'))
                    {
                        previewUrl = '~' + previewUrl;
                    }
                    else
                    {
                        previewUrl = "~/" + previewUrl;
                    }
                }

                output.Attributes.SetAttribute("href", urlHelper.Content(previewUrl));
                return;
            }

            metadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(DisplayFor);

            if (metadata.DisplayRouteValues == null)
            {
                return;
            }

            ApplyRouteValues(tagHelperContext, metadata.DisplayRouteValues);

            output.Attributes.SetAttribute("href", urlHelper.Action(metadata.DisplayRouteValues["action"].ToString(), metadata.DisplayRouteValues));
        }

        // A self closing anchor tag will be rendered using the display text
        if (output.TagMode == TagMode.SelfClosing && metadata != null)
        {
            output.TagMode = TagMode.StartTagAndEndTag;
            if (!string.IsNullOrEmpty(contentItem.DisplayText))
            {
                output.Content.Append(contentItem.DisplayText);
            }
            else
            {
                var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
                output.Content.Append(typeDefinition.ToString());
            }
        }

        return;
    }

    private static void ApplyRouteValues(TagHelperContext tagHelperContext, RouteValueDictionary route)
    {
        foreach (var attribute in tagHelperContext.AllAttributes)
        {
            if (attribute.Name.StartsWith(RoutePrefix, StringComparison.OrdinalIgnoreCase))
            {
                route.Add(attribute.Name[RoutePrefix.Length..], attribute.Value);
            }
        }
    }
}
