using System.Text.Json.Nodes;

namespace Crest.Entities;

public class Entity : IEntity
{
    public JsonObject Properties { get; set; } = [];
}
