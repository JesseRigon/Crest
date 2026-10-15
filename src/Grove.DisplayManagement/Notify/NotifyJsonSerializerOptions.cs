using System.Text.Json;

namespace Grove.DisplayManagement.Notify;

public class NotifyJsonSerializerOptions
{
    public JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions();
}
