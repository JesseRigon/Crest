using System.Text.Json;

namespace Grove.Json;

public class DocumentJsonSerializerOptions
{
    public JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions();
}
