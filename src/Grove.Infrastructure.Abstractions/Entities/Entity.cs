using System.Text.Json.Nodes;

namespace Grove.Entities;

public class Entity : IEntity
{
    public JsonObject Properties { get; set; } = [];
}
