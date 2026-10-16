using System.Text.Json.Nodes;
using Cysharp.Text;
using Fluid;
using Fluid.Filters;
using Fluid.Values;
using Microsoft.Extensions.Hosting;
using Grove.Display.Management;
using Grove.Display.Liquid;

namespace Grove.Contents.Management.Display.Liquid;

public class ConsoleLogFilter : ILiquidFilter
{
    private readonly IHostEnvironment _hostEnvironment;

    public ConsoleLogFilter(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public async ValueTask<FluidValue> ProcessAsync(FluidValue input, FilterArguments arguments, LiquidTemplateContext context)
    {
        var content = input.ToObjectValue();

        if (content == null || _hostEnvironment.IsProduction())
        {
            return NilValue.Instance;
        }

        using var sb = ZString.CreateStringBuilder();
        sb.Append("<script>console.log(");

        if (content is string stringContent)
        {
            sb.Append("\"");
            sb.Append(stringContent);
            sb.Append("\"");
        }
        else if (content is JsonNode jTokenContent)
        {
            sb.Append(jTokenContent.ToString());
        }
        else if (content is ContentItem contentItem)
        {
            sb.Append(ConvertContentItem(contentItem).ToString());
        }
        else if (content is ContentFieldSet contentFieldSet)
        {
            sb.Append(ConvertContentFieldSet(contentFieldSet).ToString());
        }
        else if (content is IShape shape)
        {
            sb.Append(shape.ShapeToJson().ToString());
        }
        else
        {
            sb.Append((await MiscFilters.Json(input, arguments, context)).ToStringValue());
        }

        sb.Append(")</script>");

        var result = new StringValue(sb.ToString(), false);

        return result;
    }

    private static JsonObject ConvertContentItem(ContentItem contentItem) => new()
    {
        // The well-known properties.
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

    private static JsonObject ConvertContentFieldSet(ContentFieldSet contentFieldSet) => new()
    {
        [nameof(ContentFieldSet.ContentItem)] = ConvertContentItem(contentFieldSet.ContentItem),
        [nameof(ContentFieldSet.Content)] = JObject.FromObject(contentFieldSet.Content),
    };
}
