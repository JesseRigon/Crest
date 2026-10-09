using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Crest;
using Crest.ContentManagement;
using Crest.ContentManagement.Display;
using Crest.DisplayManagement;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Razor;

#pragma warning disable CA1050 // Declare types in namespaces
public static class ContentPlatformRazorHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    public static async Task<IHtmlContent> DisplayAsync(this IPlatformDisplayHelper platformDisplayHelper, ContentItem content, string displayType = "", string groupId = "", IUpdateModel updater = null)
    {
        var displayManager = platformDisplayHelper.HttpContext.RequestServices.GetService<IContentItemDisplayManager>();
        var shape = await displayManager.BuildDisplayAsync(content, updater, displayType, groupId);

        return await platformDisplayHelper.DisplayHelper.ShapeExecuteAsync(shape);
    }

    /// <summary>
    /// Renders an object in the browser's console.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="content">The object to render.</param>
    /// <returns>The encoded script rendering the object to the console.</returns>
    public static IHtmlContent ConsoleLog(this IPlatformHelper platformHelper, object content)
    {
        var builder = new HtmlContentBuilder(3);

        builder.AppendHtml("<script>console.log(");

        var env = platformHelper.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();

        if (content == null || env.IsProduction())
        {
            builder.AppendHtml("null");
        }
        else if (content is string stringContent)
        {
            builder.AppendHtml("\"").Append(stringContent).AppendHtml("\"");
        }
        else if (content is JsonNode jNodeContent)
        {
            builder.AppendHtml(jNodeContent.ToString());
        }
        else if (content is ContentItem contentItem)
        {
            builder.AppendHtml(ConvertContentItem(contentItem).ToString());
        }
        else if (content is IShape shape)
        {
            builder.AppendHtml(shape.ShapeToJson().ToString());
        }
        else
        {
            builder.AppendHtml(JConvert.SerializeObject(content));
        }

        builder.AppendHtml(")</script>");

        return builder;
    }

    internal static JsonObject ConvertContentItem(ContentItem contentItem)
    {
        var o = new JsonObject
        {
            // Write all well-known properties.
            [nameof(ContentItem.ContentItemId)] = contentItem.ContentItemId,
            [nameof(ContentItem.ContentItemVersionId)] = contentItem.ContentItemVersionId,
            [nameof(ContentItem.ContentType)] = contentItem.ContentType,
            [nameof(ContentItem.DisplayText)] = contentItem.DisplayText,
            [nameof(ContentItem.Latest)] = contentItem.Latest,
            [nameof(ContentItem.Published)] = contentItem.Published,
            [nameof(ContentItem.ModifiedUtc)] = contentItem.ModifiedUtc,
            [nameof(ContentItem.PublishedUtc)] = contentItem.PublishedUtc,
            [nameof(ContentItem.CreatedUtc)] = contentItem.CreatedUtc,
            [nameof(ContentItem.Owner)] = contentItem.Owner,
            [nameof(ContentItem.Author)] = contentItem.Author,
            [nameof(ContentItem.Content)] = contentItem.Content,
        };

        return o;
    }

    internal static JsonObject ConvertContentPart(ContentPart contentPart)
    {
        var o = new JsonObject
        {
            // Write all well-known properties.
            [nameof(ContentPart.ContentItem)] = ConvertContentItem(contentPart.ContentItem),
            [nameof(ContentPart.Content)] = JObject.FromObject(contentPart.Content),
        };

        return o;
    }
}
