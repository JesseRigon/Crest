using System.Text.Json.Nodes;

namespace Grove.Entities;

public interface IEntity
{
    JsonObject Properties { get; }
}
