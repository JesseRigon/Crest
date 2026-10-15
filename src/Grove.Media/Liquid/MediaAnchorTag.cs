using System.Text.Encodings.Web;
using Fluid;
using Fluid.Ast;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Liquid.Tags;
using Grove.Liquid;
using Grove.Media.Library;

namespace Grove.Media.Liquid;

public class MediaAnchorTag : IAnchorTag
{
    public int Order => -20;

    public bool Match(IReadOnlyList<FilterArgument> argumentsList)
    {
        foreach (var argument in argumentsList)
        {
            switch (argument.Name)
            {
                case "asset-href":
                case "asset_href":
                    return true;
            }
        }

        return false;
    }

    public async ValueTask<Completion> WriteToAsync(IReadOnlyList<FilterArgument> argumentsList, IReadOnlyList<Statement> statements, TextWriter writer, TextEncoder encoder, LiquidTemplateContext context)
    {
        var services = context.Services;
        var references = services.GetRequiredService<IMediaReferenceResolver>();
        var fileVersionProvider = services.GetRequiredService<IFileVersionProvider>();
        var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();

        string assetHref = null;
        string appendVersion = null;

        Dictionary<string, string> customAttributes = null;

        foreach (var argument in argumentsList)
        {
            switch (argument.Name)
            {
                case "asset_href": assetHref = (await argument.Expression.EvaluateAsync(context)).ToStringValue(); break;
                case "append_version": appendVersion = (await argument.Expression.EvaluateAsync(context)).ToStringValue(); break;

                default:

                    customAttributes ??= [];
                    customAttributes[argument.Name] = (await argument.Expression.EvaluateAsync(context)).ToStringValue();

                    break;
            }
        }

        if (string.IsNullOrEmpty(assetHref))
        {
            return Completion.Normal;
        }

        // A placement id, a /files or /media URL, or a media library path (ruling F1).
        var resolvedUrl = await references.GetUrlAsync(assetHref);
        customAttributes ??= [];

        if (appendVersion != null && fileVersionProvider != null)
        {
            customAttributes["href"] = fileVersionProvider.AddFileVersionToPath(httpContextAccessor.HttpContext.Request.PathBase, resolvedUrl);
        }
        else
        {
            customAttributes["href"] = resolvedUrl;
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

        tagBuilder.RenderEndTag().WriteTo(writer, (HtmlEncoder)encoder);

        return Completion.Normal;
    }
}
