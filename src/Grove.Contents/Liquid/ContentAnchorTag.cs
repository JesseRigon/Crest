using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.DisplayManagement.Liquid.Tags;
using Grove.Liquid;

namespace Grove.Contents.Liquid;

public class ContentAnchorTag : IAnchorTag
{
    public int Order => -10;

    public bool Match(IReadOnlyList<FilterArgument> argumentsList)
    {
        foreach (var argument in argumentsList)
        {
            switch (argument.Name)
            {
                case "admin_for":
                case "display_for":
                case "edit_for":
                case "create_for": return true;
            }
        }

        return false;
    }

    public async ValueTask<Completion> WriteToAsync(IReadOnlyList<FilterArgument> argumentsList, IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, LiquidTemplateContext context)
    {
        var services = context.Services;
        var viewContext = context.ViewContext;
        var urlHelperFactory = services.GetRequiredService<IUrlHelperFactory>();
        var contentManager = services.GetRequiredService<IContentManager>();

        ContentItem adminFor = null;
        ContentItem displayFor = null;
        ContentItem editFor = null;
        ContentItem createFor = null;

        Dictionary<string, string> routeValues = null;
        Dictionary<string, string> customAttributes = [];

        foreach (var argument in argumentsList)
        {
            switch (argument.Name)
            {
                case "admin_for": adminFor = (await argument.Expression.EvaluateAsync(context)).ToObjectValue() as ContentItem; break;
                case "display_for": displayFor = (await argument.Expression.EvaluateAsync(context)).ToObjectValue() as ContentItem; break;
                case "edit_for": editFor = (await argument.Expression.EvaluateAsync(context)).ToObjectValue() as ContentItem; break;
                case "create_for": createFor = (await argument.Expression.EvaluateAsync(context)).ToObjectValue() as ContentItem; break;

                default:

                    if (argument.Name.StartsWith("route_", StringComparison.OrdinalIgnoreCase))
                    {
                        routeValues ??= [];
                        routeValues[argument.Name[6..]] = (await argument.Expression.EvaluateAsync(context)).ToStringValue();
                    }
                    else
                    {
                        customAttributes[argument.Name] = (await argument.Expression.EvaluateAsync(context)).ToStringValue();
                    }

                    break;
            }
        }

        ContentItem contentItem = null;

        var urlHelper = urlHelperFactory.GetUrlHelper(viewContext);

        if (displayFor != null)
        {
            contentItem = displayFor;
            var previewAspect = await contentManager.PopulateAspectAsync<PreviewAspect>(contentItem);

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

                customAttributes["href"] = urlHelper.Content(previewUrl);
            }
            else
            {
                var metadata = await contentManager.PopulateAspectAsync<ContentItemMetadata>(displayFor);

                if (metadata.DisplayRouteValues != null)
                {
                    if (routeValues != null)
                    {
                        foreach (var attribute in routeValues)
                        {
                            metadata.DisplayRouteValues.Add(attribute.Key, attribute.Value);
                        }
                    }

                    customAttributes["href"] = urlHelper.Action(metadata.DisplayRouteValues["action"].ToString(), metadata.DisplayRouteValues);
                }
            }
        }
        else if (editFor != null || adminFor != null || createFor != null)
        {
            // The admin pages are the native ones of the page registry, under the admin prefix;
            // route_* arguments travel as the query.
            var adminPrefix = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Grove.Admin.AdminOptions>>().Value.AdminUrlPrefix;
            contentItem = editFor ?? adminFor ?? createFor;
            var path = editFor != null
                ? ContentAdminPaths.Edit(editFor.ContentItemId)
                : adminFor != null
                    ? ContentAdminPaths.Edit(adminFor.ContentItemId)
                    : ContentAdminPaths.Create(createFor.ContentType);
            var url = urlHelper.Content("~" + ContentAdminPaths.InAdmin(adminPrefix, path));
            if (routeValues != null)
            {
                url = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url, routeValues);
            }

            customAttributes["href"] = url;
        }

        if (customAttributes.Count == 0)
        {
            return Completion.Normal;
        }

        var tagBuilder = new TagBuilder("a");

        foreach (var attribute in customAttributes)
        {
            tagBuilder.Attributes[attribute.Key] = attribute.Value;
        }

        tagBuilder.RenderStartTag().WriteTo(writer, (HtmlEncoder)encoder);

        if (statements != null && statements.Count > 0)
        {
            var completion = await statements.RenderStatementsAsync(writer, encoder, context);

            if (completion != Completion.Normal)
            {
                return completion;
            }
        }
        else if (!string.IsNullOrEmpty(contentItem.DisplayText))
        {
            writer.Write(encoder.Encode(contentItem.DisplayText));
        }
        else
        {
            var contentDefinitionManager = services.GetRequiredService<IContentDefinitionManager>();
            var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
            writer.Write(encoder.Encode(typeDefinition.ToString()));
        }

        tagBuilder.RenderEndTag().WriteTo(writer, (HtmlEncoder)encoder);

        return Completion.Normal;
    }
}
