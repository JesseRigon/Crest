using System.Text.Json;

namespace Crest.DisplayManagement.Notify;

public class NotifyJsonSerializerOptions
{
    public JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions();
}
