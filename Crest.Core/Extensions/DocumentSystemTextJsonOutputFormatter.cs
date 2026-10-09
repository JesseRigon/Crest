using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Crest.Data.Documents;
using Crest.Entities;

namespace Crest.Json;

public sealed class DocumentSystemTextJsonOutputFormatter : SystemTextJsonOutputFormatter
{
    public DocumentSystemTextJsonOutputFormatter(JsonSerializerOptions jsonSerializerOptions)
        : base(jsonSerializerOptions)
    {
    }

    protected override bool CanWriteType(Type type)
        => typeof(IDocument).IsAssignableFrom(type) ||
        typeof(IEntity).IsAssignableFrom(type);
}
