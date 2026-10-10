using System.Text.Json;

namespace Crest.Json;

public class DocumentJsonSerializerOptions
{
    public JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions();
}
