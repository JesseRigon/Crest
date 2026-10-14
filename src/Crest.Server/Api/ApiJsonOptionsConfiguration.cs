using System.Text.Json;
using System.Text.Json.Serialization;
using Crest.Data.Documents;
using Crest.Entities;
using Crest.Json;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Crest.Api;

/// <summary>
/// The JSON of the registry's endpoints (docs/MVC/api.md › Hosting, H2): documents and entities
/// serialize with the document serializer options, as the MVC output formatter writes them;
/// everything else uses the web defaults.
/// </summary>
internal sealed class ApiJsonOptionsConfiguration(IOptions<DocumentJsonSerializerOptions> documentOptions) : IConfigureOptions<HttpJsonOptions>
{
    public void Configure(HttpJsonOptions options) =>
        options.SerializerOptions.Converters.Insert(0, new DocumentJsonConverterFactory(documentOptions.Value.SerializerOptions));

    internal sealed class DocumentJsonConverterFactory(JsonSerializerOptions documentOptions) : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeof(IDocument).IsAssignableFrom(typeToConvert) || typeof(IEntity).IsAssignableFrom(typeToConvert);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(DocumentConverter<>).MakeGenericType(typeToConvert), documentOptions)!;
    }

    private sealed class DocumentConverter<T>(JsonSerializerOptions documentOptions) : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            (T?)JsonSerializer.Deserialize(ref reader, typeToConvert, documentOptions);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(T), documentOptions);
    }
}
