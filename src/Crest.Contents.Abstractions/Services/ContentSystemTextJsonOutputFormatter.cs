using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Crest.ContentManagement;

namespace Crest.Contents.Services;

public sealed class ContentSystemTextJsonOutputFormatter : SystemTextJsonOutputFormatter
{
    public ContentSystemTextJsonOutputFormatter(JsonSerializerOptions jsonSerializerOptions)
        : base(jsonSerializerOptions)
    {
    }

    protected override bool CanWriteType(Type type)
        => typeof(IContent).IsAssignableFrom(type);
}
